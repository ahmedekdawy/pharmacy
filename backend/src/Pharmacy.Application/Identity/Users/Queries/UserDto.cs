namespace Pharmacy.Application.Identity.Users.Queries;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullNameEn,
    string FullNameAr,
    bool IsActive,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> RoleIds);
