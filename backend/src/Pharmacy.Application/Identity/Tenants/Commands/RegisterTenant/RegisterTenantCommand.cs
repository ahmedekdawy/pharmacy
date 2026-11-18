using MediatR;
using Pharmacy.Application.Identity.Auth.Commands.Login;

namespace Pharmacy.Application.Identity.Tenants.Commands.RegisterTenant;

public sealed record RegisterTenantCommand(
    string TenantCode,
    string TenantNameEn,
    string TenantNameAr,
    string AdminFullNameEn,
    string AdminFullNameAr,
    string AdminEmail,
    string? AdminPhoneNumber,
    string AdminPassword) : IRequest<LoginResult>;
