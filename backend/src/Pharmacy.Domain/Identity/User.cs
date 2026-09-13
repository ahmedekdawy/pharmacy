using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Identity;

public class User : TenantOwnedEntity
{
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FullNameAr { get; set; } = default!;
    public string FullNameEn { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
