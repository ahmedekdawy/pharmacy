using FluentValidation;

namespace Pharmacy.Application.Identity.Users.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
        RuleFor(x => x.FullNameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.FullNameAr).NotEmpty().MaximumLength(256);
    }
}
