using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Application.Identity.Roles.Queries;

namespace Pharmacy.Application.Identity.Roles.Queries.GetRoles;

public sealed class GetRolesQueryHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleDto>>
{
    public async Task<IReadOnlyList<RoleDto>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var roles = await db.Roles.AsNoTracking()
            .OrderBy(x => x.NameEn)
            .Select(x => new { x.Id, x.Code, x.NameEn, x.NameAr, x.IsActive })
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(x => x.Id).ToList();

        var permissions = await (
            from rp in db.RolePermissions.AsNoTracking()
            join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where roleIds.Contains(rp.RoleId)
            select new { rp.RoleId, p.Code }
        ).ToListAsync(cancellationToken);

        var pages = await (
            from rp in db.RolePages.AsNoTracking()
            join p in db.Pages.AsNoTracking() on rp.PageId equals p.Id
            where roleIds.Contains(rp.RoleId)
            select new { rp.RoleId, p.Code }
        ).ToListAsync(cancellationToken);

        return roles.Select(r => new RoleDto(
            r.Id,
            r.Code,
            r.NameEn,
            r.NameAr,
            r.IsActive,
            permissions.Where(x => x.RoleId == r.Id).Select(x => x.Code).Distinct().ToList(),
            pages.Where(x => x.RoleId == r.Id).Select(x => x.Code).Distinct().ToList()
        )).ToList();
    }
}
