namespace Pharmacy.Domain.Catalog;

/// <summary>
/// Unit a product is sold/purchased in. Stock is always stored in the smallest unit (Unit / حبة).
/// </summary>
public enum SaleUnit
{
    Box = 1,   // علبة
    Strip = 2, // شريط
    Unit = 3   // حبة / وحدة
}
