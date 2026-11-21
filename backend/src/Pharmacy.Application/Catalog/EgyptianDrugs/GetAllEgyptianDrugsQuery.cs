using MediatR;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Catalog.EgyptianDrugs;

/// <summary>Bulk fetch of the whole catalog, for clients that cache it to search offline.</summary>
public sealed record GetAllEgyptianDrugsQuery : IRequest<IReadOnlyList<EgyptianDrugDto>>;

public sealed class GetAllEgyptianDrugsQueryHandler(IEgyptianDrugCatalog catalog)
    : IRequestHandler<GetAllEgyptianDrugsQuery, IReadOnlyList<EgyptianDrugDto>>
{
    public Task<IReadOnlyList<EgyptianDrugDto>> Handle(GetAllEgyptianDrugsQuery request, CancellationToken cancellationToken)
        => catalog.GetAllAsync(cancellationToken);
}
