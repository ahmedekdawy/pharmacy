using FluentValidation;

namespace Pharmacy.Application.Inventory.Commands.CreateOpeningBalance;

public sealed class CreateOpeningBalanceCommandValidator : AbstractValidator<CreateOpeningBalanceCommand>
{
    public CreateOpeningBalanceCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
