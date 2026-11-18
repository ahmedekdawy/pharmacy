using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Application.Inventory.Queries;

public sealed record OpeningBalanceLineDto(
    Guid Id,
    DateTimeOffset CreatedAt,
    Guid LocationId,
    string LocationNameEn,
    Guid ProductId,
    string ProductCode,
    string ProductNameEn,
    string ProductNameAr,
    string? Barcode,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    /// <summary>Quantity in smallest units (pills).</summary>
    decimal BaseQuantity,
    int UnitsPerBox,
    /// <summary>Cost value (base quantity × cost per smallest unit).</summary>
    decimal CostValue,
    /// <summary>Selling value at box price.</summary>
    decimal SellingValue);

/// <summary>حصر رصيد أول المدة — every opening balance line entered, for review and printing.</summary>
public sealed record GetOpeningBalancesQuery(Guid? LocationId = null) : IRequest<IReadOnlyList<OpeningBalanceLineDto>>;

public sealed class GetOpeningBalancesQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetOpeningBalancesQuery, IReadOnlyList<OpeningBalanceLineDto>>
{
    public async Task<IReadOnlyList<OpeningBalanceLineDto>> Handle(GetOpeningBalancesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var txs = db.InventoryTransactions.AsNoTracking()
            .Where(x => x.TransactionType == InventoryTransactionType.OpeningBalance);
        if (request.LocationId.HasValue) txs = txs.Where(x => x.LocationId == request.LocationId);

        var rows = await (
            from t in txs
            join p in db.Products.AsNoTracking() on t.ProductId equals p.Id
            join l in db.Locations.AsNoTracking() on t.LocationId equals l.Id
            join b in db.ProductBatches.AsNoTracking() on t.BatchId equals b.Id into batches
            from b in batches.DefaultIfEmpty()
            orderby t.CreatedAt descending
            select new
            {
                t.Id, t.CreatedAt, t.LocationId, LocationNameEn = l.NameEn, t.ProductId, p.Code, p.NameEn, p.NameAr, p.Barcode,
                BatchNumber = b != null ? b.BatchNumber : null,
                ExpiryDate = b != null ? b.ExpiryDate : null,
                t.Quantity,
                Cost = b != null ? b.PurchasePrice : 0,
                p.SellingPrice, p.StripsPerBox, p.UnitsPerStrip
            }).ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            var perBox = Math.Max(1, x.StripsPerBox) * Math.Max(1, x.UnitsPerStrip);
            return new OpeningBalanceLineDto(x.Id, x.CreatedAt, x.LocationId, x.LocationNameEn, x.ProductId, x.Code, x.NameEn,
                x.NameAr, x.Barcode, x.BatchNumber, x.ExpiryDate, x.Quantity, perBox,
                Math.Round(x.Quantity * x.Cost, 2), Math.Round(x.Quantity / perBox * x.SellingPrice, 2));
        }).ToList();
    }
}
