using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Catalog;

public class Product : TenantOwnedEntity
{
    public string Code { get; set; } = default!;
    public string? Barcode { get; set; }
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }

    /// <summary>Price of one full box (علبة).</summary>
    public decimal SellingPrice { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Number of strips (شرائط) in one box.</summary>
    public int StripsPerBox { get; set; } = 1;

    /// <summary>Number of units/pills (حبات) in one strip.</summary>
    public int UnitsPerStrip { get; set; } = 1;

    public bool AllowStripSale { get; set; }
    public bool AllowUnitSale { get; set; }

    /// <summary>Optional explicit strip price; when null it is derived from the box price.</summary>
    public decimal? StripPrice { get; set; }

    /// <summary>Optional explicit unit price; when null it is derived from the box price.</summary>
    public decimal? UnitPrice { get; set; }

    public int UnitsPerBox => Math.Max(1, StripsPerBox) * Math.Max(1, UnitsPerStrip);

    /// <summary>How many smallest units one <paramref name="unit"/> represents.</summary>
    public int FactorFor(SaleUnit unit) => unit switch
    {
        SaleUnit.Box => UnitsPerBox,
        SaleUnit.Strip => Math.Max(1, UnitsPerStrip),
        _ => 1
    };

    public bool CanSellIn(SaleUnit unit) => unit switch
    {
        SaleUnit.Box => true,
        SaleUnit.Strip => AllowStripSale,
        SaleUnit.Unit => AllowUnitSale,
        _ => false
    };

    public decimal PriceFor(SaleUnit unit) => unit switch
    {
        SaleUnit.Box => SellingPrice,
        SaleUnit.Strip => StripPrice ?? Math.Round(SellingPrice / Math.Max(1, StripsPerBox), 2),
        _ => UnitPrice ?? Math.Round(SellingPrice / UnitsPerBox, 2)
    };
}
