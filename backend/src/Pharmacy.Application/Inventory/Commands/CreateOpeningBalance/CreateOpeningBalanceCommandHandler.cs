using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Application.Inventory.Commands.CreateOpeningBalance;

public sealed class CreateOpeningBalanceCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant) : IRequestHandler<CreateOpeningBalanceCommand, Guid>
{
    public async Task<Guid> Handle(CreateOpeningBalanceCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null)
        {
            throw new InvalidOperationException("Tenant is required.");
        }

        var tenantId = currentTenant.TenantId.Value;

        // Idempotent: a line counted on an offline device and uploaded twice is applied once.
        if (request.Id.HasValue && await db.InventoryTransactions.AnyAsync(x => x.Id == request.Id, cancellationToken))
        {
            return request.Id.Value;
        }

        var locationExists = await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken);
        if (!locationExists)
        {
            throw new InvalidOperationException("Location not found.");
        }

        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Product not found.");

        // Stock is stored in smallest units (pills); cost per smallest unit.
        var factor = product.FactorFor(request.Unit);
        var baseQuantity = request.Quantity * factor;
        var baseCost = request.PurchasePrice / factor;

        var batchNumber = request.BatchNumber.Trim();
        var batch = await db.ProductBatches.FirstOrDefaultAsync(
            x => x.ProductId == request.ProductId && x.BatchNumber == batchNumber,
            cancellationToken);

        if (batch is null)
        {
            batch = new ProductBatch
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                BatchNumber = batchNumber,
                ExpiryDate = request.ExpiryDate,
                PurchasePrice = baseCost,
                AvailableQuantity = baseQuantity,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.ProductBatches.Add(batch);
        }
        else
        {
            batch.AvailableQuantity += baseQuantity;
            if (request.ExpiryDate.HasValue)
            {
                batch.ExpiryDate = request.ExpiryDate;
            }

            if (baseCost > 0)
            {
                batch.PurchasePrice = baseCost;
            }
        }

        // Batch and transaction are saved together so a retry can never double the stock.
        var tx = new InventoryTransaction
        {
            Id = request.Id ?? Guid.NewGuid(),
            TenantId = tenantId,
            LocationId = request.LocationId,
            ProductId = request.ProductId,
            BatchId = batch.Id,
            TransactionType = InventoryTransactionType.OpeningBalance,
            Quantity = baseQuantity,
            ReferenceType = "OpeningBalance",
            ReferenceId = batch.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.InventoryTransactions.Add(tx);
        await db.SaveChangesAsync(cancellationToken);
        return tx.Id;
    }
}
