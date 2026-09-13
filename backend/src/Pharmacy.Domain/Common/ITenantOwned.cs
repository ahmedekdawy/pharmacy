namespace Pharmacy.Domain.Common;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
