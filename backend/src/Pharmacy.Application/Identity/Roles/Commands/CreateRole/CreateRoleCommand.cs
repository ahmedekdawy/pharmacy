using MediatR;

namespace Pharmacy.Application.Identity.Roles.Commands.CreateRole;

public sealed record CreateRoleCommand(
    string Code,
    string NameEn,
    string NameAr,
    IReadOnlyList<Guid> PermissionIds,
    IReadOnlyList<Guid> PageIds,
    bool IsActive = true) : IRequest<Guid>;
