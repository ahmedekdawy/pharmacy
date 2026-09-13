using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Catalog;

namespace Pharmacy.Application.Catalog.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved || currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var tenantId = currentTenant.TenantId.Value;

        var codeExists = await db.Products
            .AnyAsync(x => x.TenantId == tenantId && x.Code == request.Code, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException("Product code already exists for this tenant.");
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var barcodeExists = await db.Products
                .AnyAsync(x => x.TenantId == tenantId && x.Barcode == request.Barcode, cancellationToken);

            if (barcodeExists)
            {
                throw new InvalidOperationException("Barcode already exists for this tenant.");
            }
        }

        var product = new Product
        {
            TenantId = tenantId,
            Code = request.Code.Trim(),
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            SellingPrice = request.SellingPrice,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}
