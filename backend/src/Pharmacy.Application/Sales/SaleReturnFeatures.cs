using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Application.Customers;
using Pharmacy.Domain.Catalog;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Sales;

namespace Pharmacy.Application.Sales;

public sealed record SaleReturnLineInput(Guid SaleItemId, decimal Quantity);

public sealed record CreateSaleReturnCommand(
    Guid SaleId,
    string? Notes,
    IReadOnlyList<SaleReturnLineInput> Items) : IRequest<Guid>;

public sealed record SaleReturnDto(
    Guid Id,
    string Number,
    Guid SaleId,
    string SaleNumber,
    string LocationNameEn,
    DateTimeOffset ReturnedAt,
    decimal TotalAmount);

public sealed record GetSaleReturnsQuery : IRequest<IReadOnlyList<SaleReturnDto>>;

public sealed record SaleDetailItemDto(
    Guid Id,
    Guid ProductId,
    string ProductCode,
    string ProductNameEn,
    Guid? BatchId,
    decimal Quantity,
    decimal ReturnedQuantity,
    decimal UnitPrice,
    decimal LineTotal,
    string ProductNameAr,
    SaleUnit Unit,
    decimal UnitFactor);

public sealed record SaleDetailDto(
    Guid Id,
    string Number,
    Guid LocationId,
    string LocationNameEn,
    DateTimeOffset SoldAt,
    decimal TotalAmount,
    IReadOnlyList<SaleDetailItemDto> Items,
    Guid? CustomerId,
    string? CustomerNameEn,
    string? CustomerNameAr,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string PaymentMethod,
    string Source,
    string? Notes);

public sealed record GetSaleDetailQuery(Guid SaleId) : IRequest<SaleDetailDto>;

public sealed class CreateSaleReturnCommandValidator : AbstractValidator<CreateSaleReturnCommand>
{
    public CreateSaleReturnCommandValidator()
    {
        RuleFor(x => x.SaleId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SaleItemId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

public sealed class GetSaleReturnsQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetSaleReturnsQuery, IReadOnlyList<SaleReturnDto>>
{
    public async Task<IReadOnlyList<SaleReturnDto>> Handle(GetSaleReturnsQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        return await (
            from r in db.SaleReturns.AsNoTracking()
            join s in db.Sales.AsNoTracking() on r.SaleId equals s.Id
            join l in db.Locations.AsNoTracking() on r.LocationId equals l.Id
            orderby r.ReturnedAt descending
            select new SaleReturnDto(r.Id, r.Number, s.Id, s.Number, l.NameEn, r.ReturnedAt, r.TotalAmount)
        ).Take(100).ToListAsync(cancellationToken);
    }
}

public sealed class GetSaleDetailQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetSaleDetailQuery, SaleDetailDto>
{
    public async Task<SaleDetailDto> Handle(GetSaleDetailQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var sale = await (
            from s in db.Sales.AsNoTracking()
            join l in db.Locations.AsNoTracking() on s.LocationId equals l.Id
            join c in db.Customers.AsNoTracking() on s.CustomerId equals c.Id into customers
            from c in customers.DefaultIfEmpty()
            where s.Id == request.SaleId
            select new
            {
                s.Id, s.Number, s.LocationId, LocationNameEn = l.NameEn, s.SoldAt, s.TotalAmount,
                s.CustomerId, CustomerNameEn = c != null ? c.NameEn : null, CustomerNameAr = c != null ? c.NameAr : null,
                s.SubTotal, s.DiscountAmount, s.PaidAmount, s.DueAmount, s.PaymentMethod, s.Source, s.Notes
            }
        ).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Sale not found.");

        var items = await (
            from i in db.SaleItems.AsNoTracking()
            join p in db.Products.AsNoTracking() on i.ProductId equals p.Id
            where i.SaleId == request.SaleId
            select new
            {
                i.Id,
                i.ProductId,
                p.Code,
                p.NameEn,
                p.NameAr,
                i.Unit,
                i.UnitFactor,
                i.BatchId,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal
            }
        ).ToListAsync(cancellationToken);

        var returnedByItem = await db.SaleReturnItems.AsNoTracking()
            .Where(x => items.Select(i => i.Id).Contains(x.SaleItemId))
            .GroupBy(x => x.SaleItemId)
            .Select(g => new { SaleItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.SaleItemId, x => x.Qty, cancellationToken);

        return new SaleDetailDto(
            sale.Id,
            sale.Number,
            sale.LocationId,
            sale.LocationNameEn,
            sale.SoldAt,
            sale.TotalAmount,
            items.Select(i => new SaleDetailItemDto(
                i.Id,
                i.ProductId,
                i.Code,
                i.NameEn,
                i.BatchId,
                i.Quantity,
                returnedByItem.GetValueOrDefault(i.Id),
                i.UnitPrice,
                i.LineTotal,
                i.NameAr,
                i.Unit,
                i.UnitFactor)).ToList(),
            sale.CustomerId,
            sale.CustomerNameEn,
            sale.CustomerNameAr,
            sale.SubTotal,
            sale.DiscountAmount,
            sale.PaidAmount,
            sale.DueAmount,
            sale.PaymentMethod,
            sale.Source,
            sale.Notes);
    }
}

public sealed class CreateSaleReturnCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<CreateSaleReturnCommand, Guid>
{
    public async Task<Guid> Handle(CreateSaleReturnCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var tenantId = currentTenant.TenantId.Value;

        var sale = await db.Sales.Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == request.SaleId, cancellationToken)
            ?? throw new InvalidOperationException("Sale not found.");

        var alreadyReturned = await db.SaleReturnItems
            .Where(x => sale.Items.Select(i => i.Id).Contains(x.SaleItemId))
            .GroupBy(x => x.SaleItemId)
            .Select(g => new { SaleItemId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.SaleItemId, x => x.Qty, cancellationToken);

        var saleReturn = new SaleReturn
        {
            TenantId = tenantId,
            Number = $"SR-{DateTime.UtcNow:yyyyMMddHHmmss}",
            SaleId = sale.Id,
            LocationId = sale.LocationId,
            ReturnedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        decimal total = 0;

        foreach (var line in request.Items)
        {
            var saleItem = sale.Items.FirstOrDefault(x => x.Id == line.SaleItemId)
                ?? throw new InvalidOperationException("Sale item not found.");

            var returnedQty = alreadyReturned.GetValueOrDefault(saleItem.Id);
            var remaining = saleItem.Quantity - returnedQty;
            if (line.Quantity > remaining)
                throw new InvalidOperationException($"Return quantity exceeds remaining quantity for item {saleItem.Id}.");

            var baseQty = line.Quantity * (saleItem.UnitFactor <= 0 ? 1 : saleItem.UnitFactor);

            if (saleItem.BatchId.HasValue)
            {
                var batch = await db.ProductBatches.FirstOrDefaultAsync(x => x.Id == saleItem.BatchId, cancellationToken)
                    ?? throw new InvalidOperationException("Batch not found.");
                batch.AvailableQuantity += baseQty;
            }

            var lineTotal = Math.Round(saleItem.UnitPrice * line.Quantity, 2);
            total += lineTotal;

            saleReturn.Items.Add(new SaleReturnItem
            {
                TenantId = tenantId,
                SaleItemId = saleItem.Id,
                ProductId = saleItem.ProductId,
                BatchId = saleItem.BatchId,
                Quantity = line.Quantity,
                BaseQuantity = baseQty,
                UnitPrice = saleItem.UnitPrice,
                LineTotal = lineTotal,
                CreatedAt = DateTimeOffset.UtcNow
            });

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                LocationId = sale.LocationId,
                ProductId = saleItem.ProductId,
                BatchId = saleItem.BatchId,
                TransactionType = InventoryTransactionType.SaleReturn,
                Quantity = baseQty,
                ReferenceType = "SaleReturn",
                ReferenceId = saleReturn.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        // The invoice-level discount is shared proportionally by the returned lines.
        if (sale.SubTotal > 0 && sale.DiscountAmount > 0)
            total = Math.Round(total * (sale.TotalAmount / sale.SubTotal), 2);

        // A return on a customer's account first reduces their debt; the rest is refunded in cash.
        decimal credit = 0;
        if (sale.CustomerId.HasValue)
        {
            var balance = (await CustomerBalances.GetAsync(db, [sale.CustomerId.Value], cancellationToken))
                .GetValueOrDefault(sale.CustomerId.Value);
            credit = Math.Clamp(balance, 0, total);
        }

        saleReturn.TotalAmount = total;
        saleReturn.CreditAmount = credit;
        saleReturn.RefundAmount = total - credit;
        db.SaleReturns.Add(saleReturn);
        await db.SaveChangesAsync(cancellationToken);
        return saleReturn.Id;
    }
}
