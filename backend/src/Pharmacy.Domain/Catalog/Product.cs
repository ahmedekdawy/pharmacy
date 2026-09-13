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
    public decimal SellingPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
