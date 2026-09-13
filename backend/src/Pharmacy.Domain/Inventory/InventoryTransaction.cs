using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public class InventoryTransaction : TenantOwnedEntity
{
    public Guid LocationId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    public InventoryTransactionType TransactionType { get; set; }
    public decimal Quantity { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
}
