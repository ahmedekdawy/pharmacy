using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Inventory;
using Pharmacy.Domain.Purchasing;

namespace Pharmacy.Application.Purchasing;

public sealed record PurchaseLineInput(
    Guid ProductId,
    string BatchNumber,
    DateOnly? ExpiryDate,
    decimal Quantity,
    decimal UnitCost);

public sealed record ReceivePurchaseCommand(
    Guid SupplierId,
    Guid LocationId,
    string? Notes,
    IReadOnlyList<PurchaseLineInput> Items) : IRequest<Guid>;

public sealed record PurchaseDto(
    Guid Id,
    string Number,
    Guid SupplierId,
    string SupplierNameEn,
    Guid LocationId,
    string LocationNameEn,
    DateTimeOffset PurchasedAt,
    decimal TotalAmount,
    string Status);

public sealed record GetPurchasesQuery : IRequest<IReadOnlyList<PurchaseDto>>;

public sealed class ReceivePurchaseCommandValidator : AbstractValidator<ReceivePurchaseCommand>
{
    public ReceivePurchaseCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.BatchNumber).NotEmpty().MaximumLength(128);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class GetPurchasesQueryHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<GetPurchasesQuery, IReadOnlyList<PurchaseDto>>
{
    public async Task<IReadOnlyList<PurchaseDto>> Handle(GetPurchasesQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        return await (
            from p in db.Purchases.AsNoTracking()
            join s in db.Suppliers.AsNoTracking() on p.SupplierId equals s.Id
            join l in db.Locations.AsNoTracking() on p.LocationId equals l.Id
            orderby p.PurchasedAt descending
            select new PurchaseDto(p.Id, p.Number, s.Id, s.NameEn, l.Id, l.NameEn, p.PurchasedAt, p.TotalAmount, p.Status)
        ).Take(100).ToListAsync(cancellationToken);
    }
}

public sealed class ReceivePurchaseCommandHandler(IPharmacyDbContext db, ICurrentTenant currentTenant)
    : IRequestHandler<ReceivePurchaseCommand, Guid>
{
    public async Task<Guid> Handle(ReceivePurchaseCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var tenantId = currentTenant.TenantId.Value;

        if (!await db.Suppliers.AnyAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Supplier not found.");
        if (!await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        var purchase = new Purchase
        {
            TenantId = tenantId,
            Number = $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}",
            SupplierId = request.SupplierId,
            LocationId = request.LocationId,
            PurchasedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes,
            Status = "Received",
            CreatedAt = DateTimeOffset.UtcNow
        };

        decimal total = 0;
        foreach (var line in request.Items)
        {
            if (!await db.Products.AnyAsync(x => x.Id == line.ProductId && x.IsActive, cancellationToken))
                throw new InvalidOperationException("Product not found.");

            var batchNumber = line.BatchNumber.Trim();
            var batch = await db.ProductBatches.FirstOrDefaultAsync(
                x => x.ProductId == line.ProductId && x.BatchNumber == batchNumber,
                cancellationToken);

            if (batch is null)
            {
                batch = new ProductBatch
                {
                    TenantId = tenantId,
                    ProductId = line.ProductId,
                    BatchNumber = batchNumber,
                    ExpiryDate = line.ExpiryDate,
                    PurchasePrice = line.UnitCost,
                    AvailableQuantity = line.Quantity,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.ProductBatches.Add(batch);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                batch.AvailableQuantity += line.Quantity;
                batch.PurchasePrice = line.UnitCost;
                if (line.ExpiryDate.HasValue) batch.ExpiryDate = line.ExpiryDate;
            }

            var lineTotal = line.Quantity * line.UnitCost;
            total += lineTotal;

            purchase.Items.Add(new PurchaseItem
            {
                TenantId = tenantId,
                ProductId = line.ProductId,
                BatchId = batch.Id,
                BatchNumber = batchNumber,
                ExpiryDate = line.ExpiryDate,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                LineTotal = lineTotal,
                CreatedAt = DateTimeOffset.UtcNow
            });

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                LocationId = request.LocationId,
                ProductId = line.ProductId,
                BatchId = batch.Id,
                TransactionType = InventoryTransactionType.Purchase,
                Quantity = line.Quantity,
                ReferenceType = "Purchase",
                ReferenceId = purchase.Id,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        purchase.TotalAmount = total;
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);
        return purchase.Id;
    }
}
