using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Identity;

namespace Pharmacy.Application.Identity.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    IPasswordService passwords) : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved || currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var tenantId = currentTenant.TenantId.Value;
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(x => x.Email.ToLower() == email, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("Email already exists for this tenant.");
        }

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            PasswordHash = passwords.Hash(request.Password),
            FullNameEn = request.FullNameEn.Trim(),
            FullNameAr = request.FullNameAr.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        if (request.RoleIds is { Count: > 0 })
        {
            var validRoleIds = await db.Roles
                .Where(x => request.RoleIds.Contains(x.Id) && x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            foreach (var roleId in validRoleIds)
            {
                db.UserRoles.Add(new UserRole
                {
                    TenantId = tenantId,
                    UserId = user.Id,
                    RoleId = roleId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return user.Id;
    }
}
