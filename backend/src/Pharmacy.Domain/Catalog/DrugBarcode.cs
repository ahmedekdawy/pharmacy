using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Catalog;

/// <summary>
/// Shared (cross-pharmacy) barcode registry: whenever any pharmacy saves a product with a barcode,
/// its public identity (names, pack size, public price) is remembered here so the next pharmacy that
/// scans the same box gets the data filled in automatically. No stock, cost or tenant data is stored.
/// </summary>
public class DrugBarcode : BaseEntity
{
    /// <summary>Normalized barcode (GTIN-13/EAN-13 when possible).</summary>
    public string Barcode { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string? ScientificName { get; set; }
    public string? Manufacturer { get; set; }
    public int StripsPerBox { get; set; } = 1;
    public int UnitsPerStrip { get; set; } = 1;
    public decimal? PublicPrice { get; set; }

    /// <summary>Where the data came from: "Pharmacy" (entered by a user) or "External".</summary>
    public string Source { get; set; } = "Pharmacy";

    /// <summary>How many pharmacies confirmed this barcode (higher = more trusted).</summary>
    public int Confirmations { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
}
