using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Identity;

public class UserRole : TenantOwnedEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    public User User { get; set; } = default!;
    public Role Role { get; set; } = default!;
}
