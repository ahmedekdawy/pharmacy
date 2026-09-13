using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.Categories.Queries.GetCategories;

public sealed record CategoryDto(Guid Id, string NameEn, string NameAr, bool IsActive);
public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;

public sealed class GetCategoriesQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        return await db.Categories.AsNoTracking()
            .OrderBy(x => x.NameEn)
            .Select(x => new CategoryDto(x.Id, x.NameEn, x.NameAr, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
