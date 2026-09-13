using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Inventory.Queries.GetStock;

public sealed record StockItemDto(
    Guid ProductId,
    string ProductCode,
    string ProductNameEn,
    Guid? BatchId,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    Guid LocationId,
    string LocationNameEn,
    decimal Quantity);

public sealed record GetStockQuery(Guid? LocationId = null, Guid? ProductId = null, string? Search = null)
    : IRequest<IReadOnlyList<StockItemDto>>;

public sealed class GetStockQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetStockQuery, IReadOnlyList<StockItemDto>>
{
    public async Task<IReadOnlyList<StockItemDto>> Handle(GetStockQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var query =
            from tx in db.InventoryTransactions.AsNoTracking()
            join p in db.Products.AsNoTracking() on tx.ProductId equals p.Id
            join l in db.Locations.AsNoTracking() on tx.LocationId equals l.Id
            join b in db.ProductBatches.AsNoTracking() on tx.BatchId equals b.Id into batches
            from b in batches.DefaultIfEmpty()
            select new { tx, p, l, b };

        if (request.LocationId.HasValue)
        {
            query = query.Where(x => x.tx.LocationId == request.LocationId);
        }

        if (request.ProductId.HasValue)
        {
            query = query.Where(x => x.tx.ProductId == request.ProductId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.p.Code.ToLower().Contains(term) ||
                x.p.NameEn.ToLower().Contains(term) ||
                x.p.NameAr.ToLower().Contains(term));
        }

        var rows = await query.ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new
            {
                x.tx.ProductId,
                x.p.Code,
                x.p.NameEn,
                BatchId = x.b != null ? x.b.Id : (Guid?)null,
                BatchNumber = x.b != null ? x.b.BatchNumber : null,
                ExpiryDate = x.b != null ? x.b.ExpiryDate : null,
                x.tx.LocationId,
                LocationNameEn = x.l.NameEn
            })
            .Select(g => new StockItemDto(
                g.Key.ProductId,
                g.Key.Code,
                g.Key.NameEn,
                g.Key.BatchId,
                g.Key.BatchNumber,
                g.Key.ExpiryDate,
                g.Key.LocationId,
                g.Key.LocationNameEn,
                g.Sum(x => SignedQuantity(x.tx.TransactionType, x.tx.Quantity))))
            .Where(x => x.Quantity != 0)
            .OrderBy(x => x.ProductNameEn)
            .ThenBy(x => x.ExpiryDate)
            .ToList();
    }

    private static decimal SignedQuantity(Domain.Inventory.InventoryTransactionType type, decimal quantity) =>
        type switch
        {
            Domain.Inventory.InventoryTransactionType.Sale or
            Domain.Inventory.InventoryTransactionType.TransferOut or
            Domain.Inventory.InventoryTransactionType.PurchaseReturn or
            Domain.Inventory.InventoryTransactionType.Expired or
            Domain.Inventory.InventoryTransactionType.Damaged => -Math.Abs(quantity),
            _ => Math.Abs(quantity)
        };
}
