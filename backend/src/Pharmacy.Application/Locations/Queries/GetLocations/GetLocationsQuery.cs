using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Locations;

namespace Pharmacy.Application.Locations.Queries.GetLocations;

public sealed record LocationDto(Guid Id, string NameEn, string NameAr, LocationType Type, bool IsActive);

public sealed record GetLocationsQuery : IRequest<IReadOnlyList<LocationDto>>;

public sealed class GetLocationsQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetLocationsQuery, IReadOnlyList<LocationDto>>
{
    public async Task<IReadOnlyList<LocationDto>> Handle(GetLocationsQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        return await db.Locations.AsNoTracking()
            .OrderBy(x => x.Type).ThenBy(x => x.NameEn)
            .Select(x => new LocationDto(x.Id, x.NameEn, x.NameAr, x.Type, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
