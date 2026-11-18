using FluentValidation;

namespace Pharmacy.Application.Catalog.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
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
