using MediatR;

namespace Pharmacy.Application.Identity.Tenants.Queries.GetTenantPublicInfo;

public sealed record GetTenantPublicInfoQuery(string TenantCode) : IRequest<TenantPublicInfoDto>;

public sealed record TenantPublicInfoDto(
    string TenantCode,
    string NameEn,
    string NameAr,
    string? WhatsApp);
