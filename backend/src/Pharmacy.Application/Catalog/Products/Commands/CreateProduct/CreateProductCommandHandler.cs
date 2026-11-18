using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Catalog;

namespace Pharmacy.Application.Catalog.Products.Commands.CreateProduct;

using Barcodes = Pharmacy.Application.Catalog.Barcodes;

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
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : Barcodes.Gs1Parser.Normalize(request.Barcode);

        // Idempotent for offline sync: the same product sent twice is created once.
        if (request.Id.HasValue && await db.Products.AnyAsync(x => x.Id == request.Id, cancellationToken))
        {
            return request.Id.Value;
        }

        var codeExists = await db.Products
            .AnyAsync(x => x.TenantId == tenantId && x.Code == request.Code, cancellationToken);

        if (codeExists)
        {
            throw new InvalidOperationException("Product code already exists for this tenant.");
        }

        if (barcode is not null)
        {
            var barcodeExists = await db.Products
                .AnyAsync(x => x.TenantId == tenantId && x.Barcode == barcode, cancellationToken);

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
            Id = request.Id ?? Guid.NewGuid(),
            Barcode = barcode,
            CategoryId = request.CategoryId,
            BrandId = request.BrandId,
            SellingPrice = request.SellingPrice,
            StripsPerBox = Math.Max(1, request.StripsPerBox),
            UnitsPerStrip = Math.Max(1, request.UnitsPerStrip),
            AllowStripSale = request.AllowStripSale && request.StripsPerBox > 1,
            AllowUnitSale = request.AllowUnitSale && request.StripsPerBox * request.UnitsPerStrip > 1,
            StripPrice = request.StripPrice,
            UnitPrice = request.UnitPrice,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Products.Add(product);
        await Barcodes.DrugBarcodeRegistry.RememberAsync(db, product, request.ScientificName, request.Manufacturer, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}
