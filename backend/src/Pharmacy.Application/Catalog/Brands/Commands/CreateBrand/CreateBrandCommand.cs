using MediatR;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Catalog;

namespace Pharmacy.Application.Catalog.Brands.Commands.CreateBrand;

public sealed record CreateBrandCommand(string NameEn, string NameAr, bool IsActive = true) : IRequest<Guid>;

public sealed class CreateBrandCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateBrandCommand, Guid>
{
    public async Task<Guid> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var brand = new Brand
        {
            TenantId = currentTenant.TenantId.Value,
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Brands.Add(brand);
        await db.SaveChangesAsync(cancellationToken);
        return brand.Id;
    }
}
