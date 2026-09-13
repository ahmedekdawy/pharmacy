using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Customers;

namespace Pharmacy.Application.Customers;

public sealed record CustomerDto(Guid Id, string Code, string NameEn, string NameAr, string? Phone, bool IsActive);
public sealed record GetCustomersQuery : IRequest<IReadOnlyList<CustomerDto>>;
public sealed record CreateCustomerCommand(string Code, string NameEn, string NameAr, string? Phone, bool IsActive = true) : IRequest<Guid>;

public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
    }
}

public sealed class GetCustomersQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetCustomersQuery, IReadOnlyList<CustomerDto>>
{
    public async Task<IReadOnlyList<CustomerDto>> Handle(GetCustomersQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        return await db.Customers.AsNoTracking()
            .OrderBy(x => x.NameEn)
            .Select(x => new CustomerDto(x.Id, x.Code, x.NameEn, x.NameAr, x.Phone, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}

public sealed class CreateCustomerCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateCustomerCommand, Guid>
{
    public async Task<Guid> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var code = request.Code.Trim();
        if (await db.Customers.AnyAsync(x => x.Code == code, cancellationToken))
            throw new InvalidOperationException("Customer code already exists.");

        var entity = new Customer
        {
            TenantId = currentTenant.TenantId.Value,
            Code = code,
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Customers.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
