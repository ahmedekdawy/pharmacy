using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Identity.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IPharmacyDbContext db,
    IPasswordService passwords,
    IJwtTokenService jwt,
    ICurrentTenant currentTenant) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantCode = request.TenantCode.Trim().ToUpperInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Code.ToUpper() == tenantCode, cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid tenant or credentials.");

        if (!tenant.IsActive)
        {
            throw new UnauthorizedAccessException("Tenant is deactivated.");
        }

        currentTenant.SetTenant(tenant.Id);

        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => !x.IsDeleted && x.TenantId == tenant.Id && x.Email.ToLower() == email,
                cancellationToken)
            ?? throw new UnauthorizedAccessException("Invalid tenant or credentials.");

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Account is deactivated.");
        }

        if (!passwords.Verify(user.PasswordHash, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid tenant or credentials.");
        }

        var roleIds = await db.UserRoles
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenant.Id && x.UserId == user.Id)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);

        var roles = await db.Roles
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.IsActive && roleIds.Contains(x.Id))
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var permissionIds = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenant.Id && roleIds.Contains(x.RoleId))
            .Select(x => x.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var permissions = await db.Permissions
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.IsActive && permissionIds.Contains(x.Id))
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);

        var pageIds = await db.RolePages
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenant.Id && roleIds.Contains(x.RoleId))
            .Select(x => x.PageId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var pages = await db.Pages
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.IsActive && pageIds.Contains(x.Id))
            .OrderBy(x => x.SortOrder)
            .Select(x => new PageAccessDto(x.Code, x.Route, x.NameEn, x.NameAr, x.SortOrder))
            .ToListAsync(cancellationToken);

        var token = jwt.CreateAccessToken(user.Id, tenant.Id, user.Email, roles, permissions, out var expiresAt);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            token,
            expiresAt,
            user.Id,
            tenant.Id,
            tenant.Code,
            user.Email,
            user.FullNameEn,
            user.FullNameAr,
            roles,
            permissions,
            pages);
    }
}
