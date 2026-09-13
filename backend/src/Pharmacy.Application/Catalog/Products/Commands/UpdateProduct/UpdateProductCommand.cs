using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Code,
    string NameAr,
    string NameEn,
    string? Barcode,
    Guid? CategoryId,
    Guid? BrandId,
    decimal SellingPrice,
    bool IsActive) : IRequest;

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
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Product not found.");

        var codeExists = await db.Products.AnyAsync(
            x => x.TenantId == tenantId && x.Code == request.Code && x.Id != request.ProductId,
            cancellationToken);
        if (codeExists)
        {
            throw new InvalidOperationException("Product code already exists for this tenant.");
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            var barcodeExists = await db.Products.AnyAsync(
                x => x.TenantId == tenantId && x.Barcode == request.Barcode && x.Id != request.ProductId,
                cancellationToken);
            if (barcodeExists)
            {
                throw new InvalidOperationException("Barcode already exists for this tenant.");
            }
        }

        product.Code = request.Code.Trim();
        product.NameAr = request.NameAr.Trim();
        product.NameEn = request.NameEn.Trim();
        product.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.SellingPrice = request.SellingPrice;
        product.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
