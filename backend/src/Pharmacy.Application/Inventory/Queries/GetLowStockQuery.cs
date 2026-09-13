using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Inventory.Queries;

public sealed record LowStockItemDto(
    Guid ProductId,
    string ProductCode,
    string ProductNameEn,
    decimal AvailableQuantity,
    decimal Threshold);

public sealed record GetLowStockQuery(decimal Threshold = 10) : IRequest<IReadOnlyList<LowStockItemDto>>;

public sealed class GetLowStockQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetLowStockQuery, IReadOnlyList<LowStockItemDto>>
{
    public async Task<IReadOnlyList<LowStockItemDto>> Handle(GetLowStockQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");
        var threshold = request.Threshold < 0 ? 0 : request.Threshold;

        var rows = await (
            from b in db.ProductBatches.AsNoTracking()
            join p in db.Products.AsNoTracking() on b.ProductId equals p.Id
            where p.IsActive
            group b by new { p.Id, p.Code, p.NameEn } into g
            select new
            {
                g.Key.Id,
                g.Key.Code,
                g.Key.NameEn,
                Qty = g.Sum(x => x.AvailableQuantity)
            }
        ).ToListAsync(cancellationToken);

        return rows
            .Where(x => x.Qty <= threshold)
            .OrderBy(x => x.Qty)
            .ThenBy(x => x.NameEn)
            .Select(x => new LowStockItemDto(x.Id, x.Code, x.NameEn, x.Qty, threshold))
            .ToList();
    }
}
