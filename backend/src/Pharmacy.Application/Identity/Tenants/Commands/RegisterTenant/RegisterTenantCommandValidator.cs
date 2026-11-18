using FluentValidation;

namespace Pharmacy.Application.Identity.Tenants.Commands.RegisterTenant;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.TenantCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.TenantNameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.TenantNameAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.AdminFullNameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.AdminFullNameAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8).MaximumLength(256);
    }
}
