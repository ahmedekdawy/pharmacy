using MediatR;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Locations;

namespace Pharmacy.Application.Locations.Commands.CreateLocation;

public sealed class CreateLocationCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<CreateLocationCommand, Guid>
{
    public async Task<Guid> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var location = new Location
        {
            TenantId = currentTenant.TenantId.Value,
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            Type = request.Type,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Locations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        return location.Id;
    }
}
