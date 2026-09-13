using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Expenses;

namespace Pharmacy.Application.Expenses;

public sealed record CreateExpenseCommand(
    Guid LocationId,
    string Category,
    string DescriptionEn,
    string DescriptionAr,
    decimal Amount,
    Guid? CashShiftId) : IRequest<Guid>;

public sealed record ExpenseDto(
    Guid Id,
    Guid LocationId,
    string LocationNameEn,
    string Category,
    string DescriptionEn,
    string DescriptionAr,
    decimal Amount,
    DateTimeOffset SpentAt);

public sealed record GetExpensesQuery : IRequest<IReadOnlyList<ExpenseDto>>;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DescriptionEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DescriptionAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class GetExpensesQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetExpensesQuery, IReadOnlyList<ExpenseDto>>
{
    public async Task<IReadOnlyList<ExpenseDto>> Handle(GetExpensesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        return await (
            from e in db.Expenses.AsNoTracking()
            join l in db.Locations.AsNoTracking() on e.LocationId equals l.Id
            orderby e.SpentAt descending
            select new ExpenseDto(
                e.Id,
                l.Id,
                l.NameEn,
                e.Category,
                e.DescriptionEn,
                e.DescriptionAr,
                e.Amount,
                e.SpentAt)
        ).Take(100).ToListAsync(cancellationToken);
    }
}

public sealed class CreateExpenseCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateExpenseCommand, Guid>
{
    public async Task<Guid> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");

        if (!await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        if (request.CashShiftId.HasValue)
        {
            var shiftOk = await db.CashShifts.AnyAsync(
                x => x.Id == request.CashShiftId && x.LocationId == request.LocationId && x.Status == "Open",
                cancellationToken);
            if (!shiftOk)
                throw new InvalidOperationException("Open cash shift not found for location.");
        }

        var expense = new Expense
        {
            TenantId = currentTenant.TenantId.Value,
            LocationId = request.LocationId,
            Category = request.Category.Trim(),
            DescriptionEn = request.DescriptionEn.Trim(),
            DescriptionAr = request.DescriptionAr.Trim(),
            Amount = request.Amount,
            SpentAt = DateTimeOffset.UtcNow,
            CashShiftId = request.CashShiftId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }
}

public sealed record UpdateExpenseCommand(
    Guid Id,
    Guid LocationId,
    string Category,
    string DescriptionEn,
    string DescriptionAr,
    decimal Amount) : IRequest;

public sealed record DeleteExpenseCommand(Guid Id) : IRequest;

public sealed class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().MaximumLength(128);
        RuleFor(x => x.DescriptionEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DescriptionAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class UpdateExpenseCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpdateExpenseCommand>
{
    public async Task Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Expenses.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Expense not found.");

        if (!await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        entity.LocationId = request.LocationId;
        entity.Category = request.Category.Trim();
        entity.DescriptionEn = request.DescriptionEn.Trim();
        entity.DescriptionAr = request.DescriptionAr.Trim();
        entity.Amount = request.Amount;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteExpenseCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteExpenseCommand>
{
    public async Task Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Expenses.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Expense not found.");
        db.Expenses.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
