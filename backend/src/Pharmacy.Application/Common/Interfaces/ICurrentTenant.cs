namespace Pharmacy.Application.Common.Interfaces;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
    bool IsResolved { get; }
    void SetTenant(Guid tenantId);
}
