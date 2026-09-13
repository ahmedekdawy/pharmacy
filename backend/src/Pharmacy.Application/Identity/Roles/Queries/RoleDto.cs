namespace Pharmacy.Application.Identity.Roles.Queries;

public sealed record RoleDto(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    bool IsActive,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Pages,
    IReadOnlyList<Guid> PermissionIds,
    IReadOnlyList<Guid> PageIds);
