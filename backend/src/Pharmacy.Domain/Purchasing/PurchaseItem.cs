using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Purchasing;

public class PurchaseItem : TenantOwnedEntity
{
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    public string BatchNumber { get; set; } = default!;
    public DateOnly? ExpiryDate { get; set; }
    /// <summary>Quantity in the purchased unit (boxes by default).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Quantity in smallest units added to stock.</summary>
    public decimal BaseQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    public Purchase Purchase { get; set; } = default!;
}
