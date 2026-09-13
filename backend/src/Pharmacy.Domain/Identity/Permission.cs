using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Identity;

/// <summary>
/// Platform-level permission catalog (not tenant-owned). Assigned to tenant roles via RolePermission.
/// </summary>
public class Permission : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string Module { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
