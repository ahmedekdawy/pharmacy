using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Application.Identity.Tenants.Queries.GetTenantPublicInfo;

public sealed class GetTenantPublicInfoQueryHandler(IPharmacyDbContext db)
    : IRequestHandler<GetTenantPublicInfoQuery, TenantPublicInfoDto>
{
    private const string WhatsAppKey = "Contact.WhatsApp";

    public async Task<TenantPublicInfoDto> Handle(GetTenantPublicInfoQuery request, CancellationToken cancellationToken)
    {
        var tenantCode = request.TenantCode.Trim().ToUpperInvariant();

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.IsActive && x.Code.ToUpper() == tenantCode, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        var whatsApp = await db.TenantSettings
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenant.Id && x.Key == WhatsAppKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return new TenantPublicInfoDto(tenant.Code, tenant.NameEn, tenant.NameAr, whatsApp);
    }
}
