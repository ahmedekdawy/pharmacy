using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.Brands.Queries.GetBrands;

public sealed record BrandDto(Guid Id, string NameEn, string NameAr, bool IsActive);
public sealed record GetBrandsQuery : IRequest<IReadOnlyList<BrandDto>>;

public sealed class GetBrandsQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetBrandsQuery, IReadOnlyList<BrandDto>>
{
    public async Task<IReadOnlyList<BrandDto>> Handle(GetBrandsQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        return await db.Brands.AsNoTracking()
            .OrderBy(x => x.NameEn)
            .Select(x => new BrandDto(x.Id, x.NameEn, x.NameAr, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
