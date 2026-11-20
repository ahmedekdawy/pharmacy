using MediatR;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Settings;

namespace Pharmacy.Application.Branding;

public enum BrandingKind { Logo, Banner }

public sealed record UploadTenantBrandingCommand(BrandingKind Kind, string FileName, string ContentType, Stream Content)
    : IRequest<string>;

public sealed record GetTenantBrandingQuery(string TenantCode, BrandingKind Kind) : IRequest<TeraboxFile?>;

internal static class BrandingSettingKeys
{
    public static string For(BrandingKind kind) => kind == BrandingKind.Logo ? "Branding.LogoPath" : "Branding.BannerPath";
}

public sealed class UploadTenantBrandingCommandHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    ITeraboxStorage storage) : IRequestHandler<UploadTenantBrandingCommand, string>
{
    public async Task<string> Handle(UploadTenantBrandingCommand request, CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is null) throw new InvalidOperationException("Tenant is required.");
        if (!storage.IsEnabled) throw new InvalidOperationException("Branding storage is not configured.");

        var extension = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".png";
        var kindSegment = request.Kind == BrandingKind.Logo ? "logo" : "banner";
        var relativePath = $"{currentTenant.TenantId.Value:N}/{kindSegment}{extension}";

        await storage.UploadAsync(relativePath, request.Content, request.ContentType, cancellationToken);

        var settingKey = BrandingSettingKeys.For(request.Kind);
        var existing = await db.TenantSettings.FirstOrDefaultAsync(x => x.Key == settingKey, cancellationToken);
        if (existing is null)
        {
            db.TenantSettings.Add(new TenantSetting
            {
                TenantId = currentTenant.TenantId.Value,
                Key = settingKey,
                Value = relativePath,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.Value = relativePath;
        }

        await db.SaveChangesAsync(cancellationToken);
        return relativePath;
    }
}

public sealed class GetTenantBrandingQueryHandler(IPharmacyDbContext db, ITeraboxStorage storage)
    : IRequestHandler<GetTenantBrandingQuery, TeraboxFile?>
{
    public async Task<TeraboxFile?> Handle(GetTenantBrandingQuery request, CancellationToken cancellationToken)
    {
        var tenantCode = request.TenantCode.Trim().ToUpperInvariant();
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.IsActive && x.Code.ToUpper() == tenantCode, cancellationToken);
        if (tenant is null) return null;

        var settingKey = BrandingSettingKeys.For(request.Kind);
        var relativePath = await db.TenantSettings
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted && x.TenantId == tenant.Id && x.Key == settingKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        return await storage.DownloadAsync(relativePath, cancellationToken);
    }
}
