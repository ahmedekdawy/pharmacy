using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Identity;

namespace Pharmacy.Application.Identity.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string Email,
    string FullNameEn,
    string FullNameAr,
    string? Password,
    IReadOnlyList<Guid>? RoleIds,
    bool IsActive) : IRequest;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullNameEn).NotEmpty().MaximumLength(256);
        RuleFor(x => x.FullNameAr).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).MinimumLength(8).MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Password));
    }
}

public sealed class UpdateUserCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    IPasswordService passwords) : IRequestHandler<UpdateUserCommand>
{
    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved || currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var email = request.Email.Trim().ToLowerInvariant();
        var emailTaken = await db.Users.AnyAsync(
            x => x.Id != request.UserId && x.Email.ToLower() == email,
            cancellationToken);
        if (emailTaken)
        {
            throw new InvalidOperationException("Email already exists for this tenant.");
        }

        user.Email = email;
        user.FullNameEn = request.FullNameEn.Trim();
        user.FullNameAr = request.FullNameAr.Trim();
        user.IsActive = request.IsActive;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = passwords.Hash(request.Password);
        }

        var existingRoles = db.UserRoles.Where(x => x.UserId == user.Id);
        await existingRoles.ExecuteDeleteAsync(cancellationToken);

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
                    TenantId = currentTenant.TenantId.Value,
                    UserId = user.Id,
                    RoleId = roleId,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
