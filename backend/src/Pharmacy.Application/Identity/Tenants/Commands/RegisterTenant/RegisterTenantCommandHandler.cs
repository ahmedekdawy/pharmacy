using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Application.Identity.Auth.Commands.Login;
using Pharmacy.Domain.Identity;
using Pharmacy.Domain.Locations;
using Pharmacy.Domain.Tenants;

namespace Pharmacy.Application.Identity.Tenants.Commands.RegisterTenant;

public sealed class RegisterTenantCommandHandler(
    IPharmacyDbContext db,
    IPasswordService passwords,
    IJwtTokenService jwt,
    ICurrentTenant currentTenant) : IRequestHandler<RegisterTenantCommand, LoginResult>
{
    public async Task<LoginResult> Handle(RegisterTenantCommand request, CancellationToken cancellationToken)
    {
        var tenantCode = request.TenantCode.Trim().ToUpperInvariant();
        var email = request.AdminEmail.Trim().ToLowerInvariant();

        var codeTaken = await db.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(x => !x.IsDeleted && x.Code.ToUpper() == tenantCode, cancellationToken);
        if (codeTaken)
        {
            throw new InvalidOperationException("Tenant code already exists.");
        }

        var tenant = new Tenant
        {
            Code = tenantCode,
            NameEn = request.TenantNameEn.Trim(),
            NameAr = request.TenantNameAr.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Tenants.Add(tenant);

        currentTenant.SetTenant(tenant.Id);

        var role = new Role
        {
            TenantId = tenant.Id,
            Code = "Owner",
            NameEn = "Owner",
            NameAr = "المالك",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Roles.Add(role);

        var user = new User
        {
            TenantId = tenant.Id,
            Email = email,
            PhoneNumber = PhoneNumbers.Normalize(request.AdminPhoneNumber),
            PasswordHash = passwords.Hash(request.AdminPassword),
            FullNameEn = request.AdminFullNameEn.Trim(),
            FullNameAr = request.AdminFullNameAr.Trim(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);

        db.UserRoles.Add(new UserRole
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            RoleId = role.Id,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var permissions = await db.Permissions
            .IgnoreQueryFilters()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var permission in permissions)
        {
            db.RolePermissions.Add(new RolePermission
            {
                TenantId = tenant.Id,
                RoleId = role.Id,
                PermissionId = permission.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var pages = await db.Pages
            .IgnoreQueryFilters()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        foreach (var page in pages)
        {
            db.RolePages.Add(new RolePage
            {
                TenantId = tenant.Id,
                RoleId = role.Id,
                PageId = page.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        db.Locations.Add(new Location
        {
            TenantId = tenant.Id,
            NameEn = "Main Branch",
            NameAr = "الفرع الرئيسي",
            Type = LocationType.Branch,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var roleCodes = new List<string> { role.Code };
        var permissionCodes = permissions.Select(x => x.Code).ToList();
        var pageAccess = pages
            .Select(x => new PageAccessDto(x.Code, x.Route, x.NameEn, x.NameAr, x.SortOrder))
            .ToList();

        var token = jwt.CreateAccessToken(user.Id, tenant.Id, user.Email, roleCodes, permissionCodes, out var expiresAt);

        return new LoginResult(
            token,
            expiresAt,
            user.Id,
            tenant.Id,
            tenant.Code,
            user.Email,
            user.FullNameEn,
            user.FullNameAr,
            roleCodes,
            permissionCodes,
            pageAccess);
    }
}
