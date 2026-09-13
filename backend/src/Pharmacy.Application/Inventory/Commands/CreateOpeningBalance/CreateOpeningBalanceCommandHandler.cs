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

        var locationExists = await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken);
        if (!locationExists)
        {
            throw new InvalidOperationException("Location not found.");
        }

        var productExists = await db.Products.AnyAsync(x => x.Id == request.ProductId && x.IsActive, cancellationToken);
        if (!productExists)
        {
            throw new InvalidOperationException("Product not found.");
        }

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
                PurchasePrice = request.PurchasePrice,
                AvailableQuantity = request.Quantity,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.ProductBatches.Add(batch);
        }
        else
        {
            batch.AvailableQuantity += request.Quantity;
            if (request.ExpiryDate.HasValue)
            {
                batch.ExpiryDate = request.ExpiryDate;
            }

            batch.PurchasePrice = request.PurchasePrice;
        }

        await db.SaveChangesAsync(cancellationToken);

        var tx = new InventoryTransaction
        {
            TenantId = tenantId,
            LocationId = request.LocationId,
            ProductId = request.ProductId,
            BatchId = batch.Id,
            TransactionType = InventoryTransactionType.OpeningBalance,
            Quantity = request.Quantity,
            ReferenceType = "OpeningBalance",
            ReferenceId = batch.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.InventoryTransactions.Add(tx);
        await db.SaveChangesAsync(cancellationToken);
        return tx.Id;
    }
}
