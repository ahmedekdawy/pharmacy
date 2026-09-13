using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Identity;

public class RolePermission : TenantOwnedEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    public Role Role { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
}
