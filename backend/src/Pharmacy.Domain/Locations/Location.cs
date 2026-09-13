using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Locations;

public class Location : TenantOwnedEntity
{
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public LocationType Type { get; set; }
    public bool IsActive { get; set; } = true;
}
