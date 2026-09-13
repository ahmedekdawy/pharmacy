using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Purchasing;

public class Purchase : TenantOwnedEntity
{
    public string Number { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public Guid LocationId { get; set; }
    public DateTimeOffset PurchasedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Received";
    public string? Notes { get; set; }

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}
