using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Settings;

public class TenantSetting : TenantOwnedEntity
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}
