using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Catalog.Products.Queries;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Shared.Pagination;

namespace Pharmacy.Application.Catalog.Products.Queries.SearchProducts;

public sealed class SearchProductsQueryHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<SearchProductsQuery, PagedResult<ProductDto>>
{
    public async Task<PagedResult<ProductDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved || currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 ? 20 : request.PageSize > 100 ? 100 : request.PageSize;

        var query = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Each word must match some field, in any order.
            var words = request.Search.Trim().ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(8);
            foreach (var word in words)
            {
                var term = word;
                query = query.Where(x =>
                    x.Code.ToLower().Contains(term) ||
                    x.NameAr.ToLower().Contains(term) ||
                    x.NameEn.ToLower().Contains(term) ||
                    (x.Barcode != null && x.Barcode.ToLower().Contains(term)));
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.NameEn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductDto(
                x.Id,
                x.Code,
                x.NameAr,
                x.NameEn,
                x.Barcode,
                x.CategoryId,
                x.BrandId,
                x.SellingPrice,
                x.IsActive,
                x.StripsPerBox,
                x.UnitsPerStrip,
                x.AllowStripSale,
                x.AllowUnitSale,
                x.StripPrice,
                x.UnitPrice))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
