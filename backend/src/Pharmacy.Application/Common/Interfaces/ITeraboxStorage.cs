namespace Pharmacy.Application.Common.Interfaces;

public sealed record TeraboxFile(Stream Content, string ContentType);

/// <summary>
/// Stores and retrieves branding assets (tenant logo/banner) on a personal Terabox account,
/// via the same unofficial web endpoints the Terabox web app itself calls. Terabox has no
/// official public API, so implementations of this are best-effort and may need adjusting
/// against the real account. Paths passed here are relative to the configured remote root.
/// </summary>
public interface ITeraboxStorage
{
    bool IsEnabled { get; }
    Task UploadAsync(string relativePath, Stream content, string contentType, CancellationToken cancellationToken);
    Task<TeraboxFile?> DownloadAsync(string relativePath, CancellationToken cancellationToken);
}
