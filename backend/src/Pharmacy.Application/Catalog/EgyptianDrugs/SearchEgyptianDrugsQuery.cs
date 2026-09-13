using MediatR;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.EgyptianDrugs;

public sealed record SearchEgyptianDrugsQuery(string? Search, int Limit = 20)
    : IRequest<IReadOnlyList<EgyptianDrugDto>>;

public sealed class SearchEgyptianDrugsQueryHandler(IEgyptianDrugCatalog catalog)
    : IRequestHandler<SearchEgyptianDrugsQuery, IReadOnlyList<EgyptianDrugDto>>
{
    public Task<IReadOnlyList<EgyptianDrugDto>> Handle(
        SearchEgyptianDrugsQuery request,
        CancellationToken cancellationToken)
        => catalog.SearchAsync(request.Search, request.Limit, cancellationToken);
}
