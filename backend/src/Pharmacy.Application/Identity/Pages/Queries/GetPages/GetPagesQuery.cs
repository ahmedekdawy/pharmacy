using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Identity.Pages.Queries.GetPages;

public sealed record PageDto(
    Guid Id,
    string Code,
    string Route,
    string NameEn,
    string NameAr,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);

public sealed record GetPagesQuery : IRequest<IReadOnlyList<PageDto>>;

public sealed class GetPagesQueryHandler(IPharmacyDbContext db)
    : IRequestHandler<GetPagesQuery, IReadOnlyList<PageDto>>
{
    public async Task<IReadOnlyList<PageDto>> Handle(GetPagesQuery request, CancellationToken cancellationToken)
    {
        return await db.Pages.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new PageDto(x.Id, x.Code, x.Route, x.NameEn, x.NameAr, x.ParentId, x.SortOrder, x.IsActive))
            .ToListAsync(cancellationToken);
    }
}
