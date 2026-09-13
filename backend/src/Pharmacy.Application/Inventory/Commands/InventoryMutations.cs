using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Audit;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.Application.Inventory.Commands;

public sealed record TransferStockCommand(
    Guid FromLocationId,
    Guid ToLocationId,
    Guid ProductId,
    Guid BatchId,
    decimal Quantity,
    string? Notes) : IRequest<Guid>;

public sealed record AdjustInventoryCommand(
    Guid LocationId,
    Guid ProductId,
    Guid BatchId,
    decimal QuantityDelta,
    string? Reason) : IRequest<Guid>;

public sealed class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator()
    {
        RuleFor(x => x.FromLocationId).NotEmpty();
        RuleFor(x => x.ToLocationId).NotEmpty().NotEqual(x => x.FromLocationId);
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

public sealed class AdjustInventoryCommandValidator : AbstractValidator<AdjustInventoryCommand>
{
    public AdjustInventoryCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.QuantityDelta).NotEqual(0);
    }
}

public sealed class TransferStockCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser) : IRequestHandler<TransferStockCommand, Guid>
{
    public async Task<Guid> Handle(TransferStockCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var tenantId = currentTenant.TenantId.Value;

        if (!await db.Locations.AnyAsync(x => x.Id == request.FromLocationId && x.IsActive, cancellationToken) ||
            !await db.Locations.AnyAsync(x => x.Id == request.ToLocationId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        var batch = await db.ProductBatches.FirstOrDefaultAsync(
            x => x.Id == request.BatchId && x.ProductId == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Batch not found.");

        var availableAtSource = await LocationStockAsync(db, request.FromLocationId, request.ProductId, request.BatchId, cancellationToken);
        if (availableAtSource < request.Quantity)
            throw new InvalidOperationException("Insufficient stock at source location.");

        var transferId = Guid.NewGuid();

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            TenantId = tenantId,
            LocationId = request.FromLocationId,
            ProductId = request.ProductId,
            BatchId = request.BatchId,
            TransactionType = InventoryTransactionType.TransferOut,
            Quantity = request.Quantity,
            ReferenceType = "StockTransfer",
            ReferenceId = transferId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = currentUser.UserId
        });

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            TenantId = tenantId,
            LocationId = request.ToLocationId,
            ProductId = request.ProductId,
            BatchId = request.BatchId,
            TransactionType = InventoryTransactionType.TransferIn,
            Quantity = request.Quantity,
            ReferenceType = "StockTransfer",
            ReferenceId = transferId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = currentUser.UserId
        });

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            Action = "TransferStock",
            EntityType = "StockTransfer",
            EntityId = transferId,
            NewValues = $"{{\"from\":\"{request.FromLocationId}\",\"to\":\"{request.ToLocationId}\",\"productId\":\"{request.ProductId}\",\"batchId\":\"{request.BatchId}\",\"qty\":{request.Quantity},\"notes\":{ToJson(request.Notes)}}}",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return transferId;
    }

    private static string ToJson(string? value) => value is null ? "null" : $"\"{value.Replace("\"", "\\\"")}\"";

    internal static async Task<decimal> LocationStockAsync(
        IPharmacyDbContext db,
        Guid locationId,
        Guid productId,
        Guid batchId,
        CancellationToken cancellationToken)
    {
        var txs = await db.InventoryTransactions.AsNoTracking()
            .Where(x => x.LocationId == locationId && x.ProductId == productId && x.BatchId == batchId)
            .Select(x => new { x.TransactionType, x.Quantity })
            .ToListAsync(cancellationToken);

        return txs.Sum(x => Sign(x.TransactionType, x.Quantity));
    }

    internal static decimal Sign(InventoryTransactionType type, decimal quantity) =>
        type switch
        {
            InventoryTransactionType.Sale or
            InventoryTransactionType.TransferOut or
            InventoryTransactionType.PurchaseReturn or
            InventoryTransactionType.Expired or
            InventoryTransactionType.Damaged => -Math.Abs(quantity),
            _ => Math.Abs(quantity)
        };
}

public sealed class AdjustInventoryCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser) : IRequestHandler<AdjustInventoryCommand, Guid>
{
    public async Task<Guid> Handle(AdjustInventoryCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        var tenantId = currentTenant.TenantId.Value;

        if (!await db.Locations.AnyAsync(x => x.Id == request.LocationId && x.IsActive, cancellationToken))
            throw new InvalidOperationException("Location not found.");

        var batch = await db.ProductBatches.FirstOrDefaultAsync(
            x => x.Id == request.BatchId && x.ProductId == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Batch not found.");

        if (request.QuantityDelta < 0)
        {
            var available = await TransferStockCommandHandler.LocationStockAsync(
                db, request.LocationId, request.ProductId, request.BatchId, cancellationToken);
            if (available < Math.Abs(request.QuantityDelta))
                throw new InvalidOperationException("Insufficient stock for adjustment.");
            if (batch.AvailableQuantity < Math.Abs(request.QuantityDelta))
                throw new InvalidOperationException("Insufficient batch quantity for adjustment.");
        }

        batch.AvailableQuantity += request.QuantityDelta;
        var txId = Guid.NewGuid();
        var isIncrease = request.QuantityDelta > 0;

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            Id = txId,
            TenantId = tenantId,
            LocationId = request.LocationId,
            ProductId = request.ProductId,
            BatchId = request.BatchId,
            TransactionType = isIncrease ? InventoryTransactionType.Adjustment : InventoryTransactionType.Damaged,
            Quantity = Math.Abs(request.QuantityDelta),
            ReferenceType = "InventoryAdjustment",
            ReferenceId = txId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = currentUser.UserId
        });

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = currentUser.UserId,
            Action = "AdjustInventory",
            EntityType = "InventoryTransaction",
            EntityId = txId,
            NewValues = $"{{\"locationId\":\"{request.LocationId}\",\"delta\":{request.QuantityDelta},\"reason\":{(request.Reason is null ? "null" : $"\"{request.Reason.Replace("\"", "\\\"")}\"")}}}",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return txId;
    }
}
