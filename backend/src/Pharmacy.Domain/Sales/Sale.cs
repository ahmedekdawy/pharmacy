using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public class Sale : TenantOwnedEntity
{
    public string Number { get; set; } = default!;
    public Guid LocationId { get; set; }
    public Guid? CustomerId { get; set; }
    public DateTimeOffset SoldAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Completed";
    public string? Notes { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
