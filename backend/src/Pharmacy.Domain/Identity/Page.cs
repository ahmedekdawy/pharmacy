using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Identity;

/// <summary>
/// Platform-level page/menu catalog. Assigned to tenant roles via RolePage.
/// </summary>
public class Page : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string Route { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RolePage> RolePages { get; set; } = new List<RolePage>();
}
