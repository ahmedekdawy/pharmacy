using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public class SaleReturn : TenantOwnedEntity
{
    public string Number { get; set; } = default!;
    public Guid SaleId { get; set; }
    public Guid LocationId { get; set; }
    public DateTimeOffset ReturnedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }

    public Sale Sale { get; set; } = default!;
    public ICollection<SaleReturnItem> Items { get; set; } = new List<SaleReturnItem>();
}
