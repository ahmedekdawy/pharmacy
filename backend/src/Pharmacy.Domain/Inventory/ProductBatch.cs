using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Inventory;

public class ProductBatch : TenantOwnedEntity
{
    public Guid ProductId { get; set; }
    public string BatchNumber { get; set; } = default!;
    public DateOnly? ExpiryDate { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal AvailableQuantity { get; set; }
}
