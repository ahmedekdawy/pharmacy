using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Identity;

namespace Pharmacy.Application.Identity.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<CreateRoleCommand, Guid>
{
    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved || currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var tenantId = currentTenant.TenantId.Value;
        var code = request.Code.Trim();

        if (await db.Roles.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new InvalidOperationException("Role code already exists.");
        }

        var role = new Role
        {
            TenantId = tenantId,
            Code = code,
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        var permissionIds = await db.Permissions
            .Where(x => request.PermissionIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in permissionIds)
        {
            db.RolePermissions.Add(new RolePermission
            {
                TenantId = tenantId,
                RoleId = role.Id,
                PermissionId = permissionId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var pageIds = await db.Pages
            .Where(x => request.PageIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var pageId in pageIds)
        {
            db.RolePages.Add(new RolePage
            {
                TenantId = tenantId,
                RoleId = role.Id,
                PageId = pageId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }
}
