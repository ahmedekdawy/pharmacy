using MediatR;

namespace Pharmacy.Application.Identity.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleDto>>;
