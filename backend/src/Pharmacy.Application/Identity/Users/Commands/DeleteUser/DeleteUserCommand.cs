using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Identity.Users.Commands.DeleteUser;

public sealed record DeleteUserCommand(Guid UserId) : IRequest;

public sealed class DeleteUserCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser) : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        if (currentUser.UserId == request.UserId)
        {
            throw new InvalidOperationException("You cannot delete your own account.");
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        await db.UserRoles.Where(x => x.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
    }
}
