using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.Products.Commands.UpdateProduct;

using Barcodes = Pharmacy.Application.Catalog.Barcodes;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Code,
    string NameAr,
    string NameEn,
    string? Barcode,
    Guid? CategoryId,
    Guid? BrandId,
    decimal SellingPrice,
    bool IsActive,
    int StripsPerBox = 1,
    int UnitsPerStrip = 1,
    bool AllowStripSale = false,
    bool AllowUnitSale = false,
    decimal? StripPrice = null,
    decimal? UnitPrice = null) : IRequest;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Barcode).MaximumLength(64);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StripsPerBox).InclusiveBetween(1, 1000);
        RuleFor(x => x.UnitsPerStrip).InclusiveBetween(1, 1000);
        RuleFor(x => x.StripPrice).GreaterThanOrEqualTo(0).When(x => x.StripPrice.HasValue);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).When(x => x.UnitPrice.HasValue);
    }
}

public sealed class UpdateProductCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved || currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var tenantId = currentTenant.TenantId.Value;
        var barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : Barcodes.Gs1Parser.Normalize(request.Barcode);
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Product not found.");

        var codeExists = await db.Products.AnyAsync(
            x => x.TenantId == tenantId && x.Code == request.Code && x.Id != request.ProductId,
            cancellationToken);
        if (codeExists)
        {
            throw new InvalidOperationException("Product code already exists for this tenant.");
        }

        if (barcode is not null)
        {
            var barcodeExists = await db.Products.AnyAsync(
                x => x.TenantId == tenantId && x.Barcode == barcode && x.Id != request.ProductId,
                cancellationToken);
            if (barcodeExists)
            {
                throw new InvalidOperationException("Barcode already exists for this tenant.");
            }
        }

        var oldUnitsPerBox = product.UnitsPerBox;

        product.Code = request.Code.Trim();
        product.NameAr = request.NameAr.Trim();
        product.NameEn = request.NameEn.Trim();
        product.Barcode = barcode;
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.SellingPrice = request.SellingPrice;
        product.IsActive = request.IsActive;
        product.StripsPerBox = Math.Max(1, request.StripsPerBox);
        product.UnitsPerStrip = Math.Max(1, request.UnitsPerStrip);
        product.AllowStripSale = request.AllowStripSale && product.StripsPerBox > 1;
        product.AllowUnitSale = request.AllowUnitSale && product.UnitsPerBox > 1;
        product.StripPrice = request.StripPrice;
        product.UnitPrice = request.UnitPrice;

        if (product.UnitsPerBox != oldUnitsPerBox)
        {
            await RescaleStockAsync(product.Id, (decimal)product.UnitsPerBox / oldUnitsPerBox, cancellationToken);
        }

        await Barcodes.DrugBarcodeRegistry.RememberAsync(db, product, null, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stock is kept in smallest units. When the pack size changes (e.g. a product that was
    /// tracked per box becomes 3 strips × 10 pills), existing quantities are converted so that
    /// the number of boxes on hand and their cost stay the same.
    /// </summary>
    private async Task RescaleStockAsync(Guid productId, decimal ratio, CancellationToken cancellationToken)
    {
        foreach (var batch in await db.ProductBatches.Where(x => x.ProductId == productId).ToListAsync(cancellationToken))
        {
            batch.AvailableQuantity *= ratio;
            batch.PurchasePrice /= ratio;
        }

        foreach (var tx in await db.InventoryTransactions.Where(x => x.ProductId == productId).ToListAsync(cancellationToken))
            tx.Quantity *= ratio;

        foreach (var item in await db.SaleItems.Where(x => x.ProductId == productId).ToListAsync(cancellationToken))
        {
            item.BaseQuantity *= ratio;
            item.UnitFactor *= ratio;
        }

        foreach (var item in await db.SaleReturnItems.Where(x => x.ProductId == productId).ToListAsync(cancellationToken))
            item.BaseQuantity *= ratio;

        foreach (var item in await db.PurchaseItems.Where(x => x.ProductId == productId).ToListAsync(cancellationToken))
            item.BaseQuantity *= ratio;
    }
}
