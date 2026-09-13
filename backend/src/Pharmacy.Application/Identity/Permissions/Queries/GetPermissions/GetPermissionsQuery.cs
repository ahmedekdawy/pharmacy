using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Identity.Permissions.Queries.GetPermissions;

public sealed record PermissionDto(Guid Id, string Code, string Module, string NameEn, string NameAr, bool IsActive);

public sealed record GetPermissionsQuery : IRequest<IReadOnlyList<PermissionDto>>;

public sealed class GetPermissionsQueryHandler(IPharmacyDbContext db)
    : IRequestHandler<GetPermissionsQuery, IReadOnlyList<PermissionDto>>
{
    public async Task<IReadOnlyList<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken cancellationToken)
    {
        return await db.Permissions.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Module).ThenBy(x => x.Code)
            .Select(x => new PermissionDto(x.Id, x.Code, x.Module, x.NameEn, x.NameAr, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
