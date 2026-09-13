using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public class SaleItem : TenantOwnedEntity
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Sale Sale { get; set; } = default!;
}
