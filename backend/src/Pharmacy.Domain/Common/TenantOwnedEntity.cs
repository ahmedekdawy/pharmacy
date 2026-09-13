namespace Pharmacy.Domain.Common;

public abstract class TenantOwnedEntity : AuditableEntity, ITenantOwned
{
    public Guid TenantId { get; set; }
}
