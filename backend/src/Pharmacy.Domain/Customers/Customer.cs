using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Customers;

public class Customer : TenantOwnedEntity
{
    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}
