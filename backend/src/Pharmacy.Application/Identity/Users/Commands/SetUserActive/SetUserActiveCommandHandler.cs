using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Identity.Users.Commands.SetUserActive;

public sealed class SetUserActiveCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser) : IRequestHandler<SetUserActiveCommand>
{
    public async Task Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (currentUser.UserId == user.Id && !request.IsActive)
        {
            throw new InvalidOperationException("You cannot deactivate your own account.");
        }

        user.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}
