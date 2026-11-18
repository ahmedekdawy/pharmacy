using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Customers;

namespace Pharmacy.Application.Customers;

/// <summary>
/// Customer debt = Σ sale.DueAmount − Σ return.CreditAmount − Σ payments.
/// </summary>
public static class CustomerBalances
{
    public static async Task<Dictionary<Guid, decimal>> GetAsync(
        IPharmacyDbContext db,
        IReadOnlyCollection<Guid>? customerIds,
        CancellationToken cancellationToken)
    {
        var sales = db.Sales.AsNoTracking().Where(x => x.CustomerId != null && x.DueAmount != 0);
        var payments = db.CustomerPayments.AsNoTracking();
        var credits =
            from r in db.SaleReturns.AsNoTracking()
            join s in db.Sales.AsNoTracking() on r.SaleId equals s.Id
            where s.CustomerId != null && r.CreditAmount != 0
            select new { CustomerId = s.CustomerId!.Value, r.CreditAmount };

        if (customerIds is not null)
        {
            sales = sales.Where(x => customerIds.Contains(x.CustomerId!.Value));
            payments = payments.Where(x => customerIds.Contains(x.CustomerId));
            credits = credits.Where(x => customerIds.Contains(x.CustomerId));
        }

        var due = await sales.GroupBy(x => x.CustomerId!.Value)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.DueAmount) })
            .ToListAsync(cancellationToken);
        var paid = await payments.GroupBy(x => x.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        var credited = await credits.GroupBy(x => x.CustomerId)
            .Select(g => new { g.Key, Sum = g.Sum(x => x.CreditAmount) })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, decimal>();
        foreach (var x in due) result[x.Key] = result.GetValueOrDefault(x.Key) + x.Sum;
        foreach (var x in paid) result[x.Key] = result.GetValueOrDefault(x.Key) - x.Sum;
        foreach (var x in credited) result[x.Key] = result.GetValueOrDefault(x.Key) - x.Sum;
        return result;
    }
}

public sealed record CustomerDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    string? Phone,
    string? Address,
    decimal CreditLimit,
    decimal Balance,
    bool IsActive);

public sealed record GetCustomersQuery(bool OnlyDebtors = false) : IRequest<IReadOnlyList<CustomerDto>>;

/// <param name="Id">Optional client-generated id (customers created offline).</param>
public sealed record CreateCustomerCommand(
    string Code,
    string NameEn,
    string NameAr,
    string? Phone,
    bool IsActive = true,
    string? Address = null,
    decimal CreditLimit = 0,
    Guid? Id = null) : IRequest<Guid>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Address).MaximumLength(512);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}

public sealed class GetCustomersQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetCustomersQuery, IReadOnlyList<CustomerDto>>
{
    public async Task<IReadOnlyList<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var customers = await db.Customers.AsNoTracking().OrderBy(x => x.NameEn).ToListAsync(cancellationToken);
        var balances = await CustomerBalances.GetAsync(db, null, cancellationToken);

        return customers
            .Select(x => new CustomerDto(
                x.Id, x.Code, x.NameEn, x.NameAr, x.Phone, x.Address, x.CreditLimit,
                balances.GetValueOrDefault(x.Id), x.IsActive))
            .Where(x => !request.OnlyDebtors || x.Balance > 0)
            .OrderByDescending(x => request.OnlyDebtors ? x.Balance : 0)
            .ThenBy(x => x.NameEn)
            .ToList();
    }
}

public sealed class CreateCustomerCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");

        if (request.Id.HasValue && await db.Customers.AnyAsync(x => x.Id == request.Id, cancellationToken))
            return request.Id.Value; // idempotent (offline sync retry)

        var code = request.Code.Trim();
        if (await db.Customers.AnyAsync(x => x.Code == code, cancellationToken))
        {
            if (!request.Id.HasValue) throw new InvalidOperationException("Customer code already exists.");
            code = $"{code}-{Random.Shared.Next(100, 999)}"; // offline-created duplicate code
        }

        var entity = new Customer
        {
            Id = request.Id ?? Guid.NewGuid(),
            TenantId = currentTenant.TenantId.Value,
            Code = code,
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            CreditLimit = request.CreditLimit,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Customers.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateCustomerCommand(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    string? Phone,
    bool IsActive,
    string? Address = null,
    decimal CreditLimit = 0) : IRequest;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest;

public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Address).MaximumLength(512);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCustomerCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpdateCustomerCommand>
{
    public async Task Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Customer not found.");

        var code = request.Code.Trim();
        if (await db.Customers.AnyAsync(x => x.Code == code && x.Id != request.Id, cancellationToken))
            throw new InvalidOperationException("Customer code already exists.");

        entity.Code = code;
        entity.NameEn = request.NameEn.Trim();
        entity.NameAr = request.NameAr.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        entity.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        entity.CreditLimit = request.CreditLimit;
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteCustomerCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteCustomerCommand>
{
    public async Task Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Customers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Customer not found.");

        var balance = (await CustomerBalances.GetAsync(db, [entity.Id], cancellationToken)).GetValueOrDefault(entity.Id);
        if (balance != 0)
            throw new InvalidOperationException("Cannot delete a customer with an outstanding balance.");

        db.Customers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
