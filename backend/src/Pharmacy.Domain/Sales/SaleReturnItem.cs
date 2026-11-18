using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public class SaleReturnItem : TenantOwnedEntity
{
    public Guid SaleReturnId { get; set; }
    public Guid SaleItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }

    /// <summary>Quantity in the sale item's unit.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Quantity in smallest units returned to stock.</summary>
    public decimal BaseQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public SaleReturn SaleReturn { get; set; } = default!;
}
