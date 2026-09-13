using MediatR;

namespace Pharmacy.Application.Identity.Auth.Commands.Login;

public sealed record LoginCommand(
    string TenantCode,
    string Email,
    string Password) : IRequest<LoginResult>;

public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    Guid TenantId,
    string TenantCode,
    string Email,
    string FullNameEn,
    string FullNameAr,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<PageAccessDto> Pages);

public sealed record PageAccessDto(
    string Code,
    string Route,
    string NameEn,
    string NameAr,
    int SortOrder);
