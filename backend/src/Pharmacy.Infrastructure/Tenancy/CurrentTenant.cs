using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Infrastructure.Tenancy;

public sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }
    public bool IsResolved => TenantId.HasValue;

    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}
