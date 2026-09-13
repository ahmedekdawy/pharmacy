using FluentValidation;
using Pharmacy.Domain.Locations;

namespace Pharmacy.Application.Locations.Commands.CreateLocation;

public sealed class CreateLocationCommandValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationCommandValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Type).IsInEnum();
    }
}
