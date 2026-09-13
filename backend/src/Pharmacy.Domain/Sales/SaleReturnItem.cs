using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public class SaleReturnItem : TenantOwnedEntity
{
    public Guid SaleReturnId { get; set; }
    public Guid SaleItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public SaleReturn SaleReturn { get; set; } = default!;
}
