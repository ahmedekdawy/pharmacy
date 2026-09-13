using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Identity;

public class RolePage : TenantOwnedEntity
{
    public Guid RoleId { get; set; }
    public Guid PageId { get; set; }

    public Role Role { get; set; } = default!;
    public Page Page { get; set; } = default!;
}
