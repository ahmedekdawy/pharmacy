using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
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
    decimal LineTotal);

public sealed record SaleDetailDto(
    Guid Id,
    string Number,
    Guid LocationId,
    string LocationNameEn,
    DateTimeOffset SoldAt,
    decimal TotalAmount,
    IReadOnlyList<SaleDetailItemDto> Items);

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
            where s.Id == request.SaleId
            select new { s.Id, s.Number, s.LocationId, LocationNameEn = l.NameEn, s.SoldAt, s.TotalAmount }
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
                i.LineTotal)).ToList());
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

            if (saleItem.BatchId.HasValue)
            {
                var batch = await db.ProductBatches.FirstOrDefaultAsync(x => x.Id == saleItem.BatchId, cancellationToken)
                    ?? throw new InvalidOperationException("Batch not found.");
                batch.AvailableQuantity += line.Quantity;
            }

            var lineTotal = saleItem.UnitPrice * line.Quantity;
            total += lineTotal;

            saleReturn.Items.Add(new SaleReturnItem
            {
                TenantId = tenantId,
                SaleItemId = saleItem.Id,
                ProductId = saleItem.ProductId,
                BatchId = saleItem.BatchId,
                Quantity = line.Quantity,
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
                Quantity = line.Quantity,
                ReferenceType = "SaleReturn",
                ReferenceId = saleReturn.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        saleReturn.TotalAmount = total;
        db.SaleReturns.Add(saleReturn);
        await db.SaveChangesAsync(cancellationToken);
        return saleReturn.Id;
    }
}
