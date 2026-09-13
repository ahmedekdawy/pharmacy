using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Tenants;

public class Tenant : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
