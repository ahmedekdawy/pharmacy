using Pharmacy.Domain.Catalog;
using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Sales;

public class SaleItem : TenantOwnedEntity
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }

    /// <summary>Quantity in the sold <see cref="Unit"/> (e.g. 2 strips).</summary>
    public decimal Quantity { get; set; }
    public SaleUnit Unit { get; set; } = SaleUnit.Box;

    /// <summary>Smallest units per one sold unit at the time of sale.</summary>
    public decimal UnitFactor { get; set; } = 1;

    /// <summary>Quantity in smallest units (what is deducted from stock).</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>Price per sold unit.</summary>
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Sale Sale { get; set; } = default!;
}
