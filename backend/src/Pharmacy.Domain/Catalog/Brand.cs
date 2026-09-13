using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Catalog;

public class Brand : TenantOwnedEntity
{
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
