using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Suppliers;

namespace Pharmacy.Application.Suppliers;

public sealed record SupplierDto(Guid Id, string Code, string NameEn, string NameAr, string? Phone, string? Email, bool IsActive);
public sealed record GetSuppliersQuery : IRequest<IReadOnlyList<SupplierDto>>;
public sealed record CreateSupplierCommand(string Code, string NameEn, string NameAr, string? Phone, string? Email, bool IsActive = true) : IRequest<Guid>;

public sealed class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
    }
}

public sealed class GetSuppliersQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetSuppliersQuery, IReadOnlyList<SupplierDto>>
{
    public async Task<IReadOnlyList<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        EnsureTenant(currentTenant);
        return await db.Suppliers.AsNoTracking()
            .OrderBy(x => x.NameEn)
            .Select(x => new SupplierDto(x.Id, x.Code, x.NameEn, x.NameAr, x.Phone, x.Email, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    private static void EnsureTenant(ICurrentTenant currentTenant)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
    }
}

public sealed class CreateSupplierCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateSupplierCommand, Guid>
{
    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");

        var code = request.Code.Trim();
        if (await db.Suppliers.AnyAsync(x => x.Code == code, cancellationToken))
            throw new InvalidOperationException("Supplier code already exists.");

        var entity = new Supplier
        {
            TenantId = currentTenant.TenantId.Value,
            Code = code,
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Suppliers.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}

public sealed record UpdateSupplierCommand(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    string? Phone,
    string? Email,
    bool IsActive) : IRequest;

public sealed record DeleteSupplierCommand(Guid Id) : IRequest;

public sealed class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
    }
}

public sealed class UpdateSupplierCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpdateSupplierCommand>
{
    public async Task Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Supplier not found.");

        var code = request.Code.Trim();
        if (await db.Suppliers.AnyAsync(x => x.Code == code && x.Id != request.Id, cancellationToken))
            throw new InvalidOperationException("Supplier code already exists.");

        entity.Code = code;
        entity.NameEn = request.NameEn.Trim();
        entity.NameAr = request.NameAr.Trim();
        entity.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        entity.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        entity.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteSupplierCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteSupplierCommand>
{
    public async Task Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var entity = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Supplier not found.");
        db.Suppliers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
