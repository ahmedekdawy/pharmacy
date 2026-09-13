using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Purchasing;

public class PurchaseItem : TenantOwnedEntity
{
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    public string BatchNumber { get; set; } = default!;
    public DateOnly? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    public Purchase Purchase { get; set; } = default!;
}
