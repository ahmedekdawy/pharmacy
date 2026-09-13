using Pharmacy.Domain.Common;

namespace Pharmacy.Domain.Audit;

public class AuditLog : TenantOwnedEntity
{
    public Guid? UserId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
}
