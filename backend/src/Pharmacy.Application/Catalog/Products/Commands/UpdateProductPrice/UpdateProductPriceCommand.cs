using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.Products.Commands.UpdateProductPrice;

/// <summary>
/// Updates only the box selling price (e.g. after the cashier checks the latest price against
/// an external catalog). Unlike <see cref="UpdateProduct.UpdateProductCommand"/> this never
/// touches the rest of the product, so callers that only have the price don't risk wiping
/// fields (category, brand, pack config) they never loaded.
/// </summary>
public sealed record UpdateProductPriceCommand(Guid ProductId, decimal SellingPrice) : IRequest;

public sealed class UpdateProductPriceCommandValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.SellingPrice).GreaterThan(0);
    }
}

public sealed class UpdateProductPriceCommandHandler(IPharmacyDbContext db)
    : IRequestHandler<UpdateProductPriceCommand>
{
    public async Task Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Product not found.");

        product.SellingPrice = request.SellingPrice;
        await db.SaveChangesAsync(cancellationToken);
    }
}
