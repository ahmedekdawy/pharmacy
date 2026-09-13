using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Identity;

namespace Pharmacy.Application.Identity.Roles.Commands;

public sealed record UpdateRoleCommand(
    Guid Id,
    string Code,
    string NameEn,
    string NameAr,
    IReadOnlyList<Guid> PermissionIds,
    IReadOnlyList<Guid> PageIds,
    bool IsActive) : IRequest;

public sealed record DeleteRoleCommand(Guid Id) : IRequest;

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(256);
    }
}

public sealed class UpdateRoleCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var tenantId = currentTenant.TenantId.Value;

        var role = await db.Roles.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Role not found.");

        var code = request.Code.Trim();
        if (await db.Roles.AnyAsync(x => x.Code == code && x.Id != request.Id, cancellationToken))
            throw new InvalidOperationException("Role code already exists.");

        role.Code = code;
        role.NameEn = request.NameEn.Trim();
        role.NameAr = request.NameAr.Trim();
        role.IsActive = request.IsActive;

        await db.RolePermissions.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(cancellationToken);
        await db.RolePages.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(cancellationToken);

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
    }
}

public sealed class DeleteRoleCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var role = await db.Roles.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Role not found.");

        if (string.Equals(role.Code, "Owner", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Owner role cannot be deleted.");

        var inUse = await db.UserRoles.AnyAsync(x => x.RoleId == role.Id, cancellationToken);
        if (inUse)
            throw new InvalidOperationException("Role is assigned to users and cannot be deleted.");

        await db.RolePermissions.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(cancellationToken);
        await db.RolePages.Where(x => x.RoleId == role.Id).ExecuteDeleteAsync(cancellationToken);
        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
    }
}
