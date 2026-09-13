using MediatR;
using Pharmacy.Shared.Pagination;

namespace Pharmacy.Application.Catalog.Products.Queries.SearchProducts;

public sealed record SearchProductsQuery(
    string? Search,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ProductDto>>;
