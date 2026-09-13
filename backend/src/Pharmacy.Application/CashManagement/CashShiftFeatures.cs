using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.CashManagement;

namespace Pharmacy.Application.CashManagement;

public sealed record OpenCashShiftCommand(Guid LocationId, decimal OpeningCash, string? Notes) : IRequest<Guid>;
public sealed record CloseCashShiftCommand(Guid CashShiftId, decimal ClosingCash, string? Notes) : IRequest;
public sealed record CashShiftDto(
    Guid Id,
    Guid LocationId,
    string LocationNameEn,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningCash,
    decimal? ClosingCash,
    decimal? ExpectedCash,
    string Status);

public sealed record GetCashShiftsQuery : IRequest<IReadOnlyList<CashShiftDto>>;

public sealed class OpenCashShiftCommandValidator : AbstractValidator<OpenCashShiftCommand>
{
    public OpenCashShiftCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.OpeningCash).GreaterThanOrEqualTo(0);
    }
}

public sealed class CloseCashShiftCommandValidator : AbstractValidator<CloseCashShiftCommand>
{
    public CloseCashShiftCommandValidator()
    {
        RuleFor(x => x.CashShiftId).NotEmpty();
        RuleFor(x => x.ClosingCash).GreaterThanOrEqualTo(0);
    }
}

public sealed class GetCashShiftsQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetCashShiftsQuery, IReadOnlyList<CashShiftDto>>
{
    public async Task<IReadOnlyList<CashShiftDto>> Handle(GetCashShiftsQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        return await (
            from c in db.CashShifts.AsNoTracking()
            join l in db.Locations.AsNoTracking() on c.LocationId equals l.Id
            orderby c.OpenedAt descending
            select new CashShiftDto(
                c.Id,
                l.Id,
                l.NameEn,
                c.OpenedAt,
                c.ClosedAt,
                c.OpeningCash,
                c.ClosingCash,
                c.ExpectedCash,
                c.Status)
        ).Take(100).ToListAsync(cancellationToken);
    }
}

public sealed class OpenCashShiftCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser) : IRequestHandler<OpenCashShiftCommand, Guid>
{
    public async Task<Guid> Handle(OpenCashShiftCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        if (currentUser.UserId is null) throw new InvalidOperationException("User is required.");

        if (!await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        var hasOpen = await db.CashShifts.AnyAsync(
            x => x.LocationId == request.LocationId && x.Status == "Open",
            cancellationToken);
        if (hasOpen)
            throw new InvalidOperationException("An open cash shift already exists for this location.");

        var shift = new CashShift
        {
            TenantId = currentTenant.TenantId.Value,
            LocationId = request.LocationId,
            OpenedByUserId = currentUser.UserId.Value,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningCash = request.OpeningCash,
            Status = "Open",
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.CashShifts.Add(shift);
        await db.SaveChangesAsync(cancellationToken);
        return shift.Id;
    }
}

public sealed class CloseCashShiftCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser) : IRequestHandler<CloseCashShiftCommand>
{
    public async Task Handle(CloseCashShiftCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        if (currentUser.UserId is null) throw new InvalidOperationException("User is required.");

        var shift = await db.CashShifts.FirstOrDefaultAsync(x => x.Id == request.CashShiftId, cancellationToken)
            ?? throw new InvalidOperationException("Cash shift not found.");

        if (shift.Status != "Open")
            throw new InvalidOperationException("Cash shift is already closed.");

        var salesTotal = await db.Sales
            .Where(x => x.LocationId == shift.LocationId && x.SoldAt >= shift.OpenedAt)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        var returnsTotal = await db.SaleReturns
            .Where(x => x.LocationId == shift.LocationId && x.ReturnedAt >= shift.OpenedAt)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0;

        var expensesTotal = await db.Expenses
            .Where(x => x.LocationId == shift.LocationId && x.SpentAt >= shift.OpenedAt &&
                        (x.CashShiftId == null || x.CashShiftId == shift.Id))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;

        shift.ExpectedCash = shift.OpeningCash + salesTotal - returnsTotal - expensesTotal;
        shift.ClosingCash = request.ClosingCash;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.ClosedByUserId = currentUser.UserId.Value;
        shift.Status = "Closed";
        shift.Notes = string.IsNullOrWhiteSpace(request.Notes) ? shift.Notes : request.Notes;

        await db.SaveChangesAsync(cancellationToken);
    }
}
