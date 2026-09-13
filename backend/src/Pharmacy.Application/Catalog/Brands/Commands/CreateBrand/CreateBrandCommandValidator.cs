using FluentValidation;

namespace Pharmacy.Application.Catalog.Brands.Commands.CreateBrand;

public sealed class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
    }
}
