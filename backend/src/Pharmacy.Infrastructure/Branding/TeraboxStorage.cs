using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Infrastructure.Branding;

/// <summary>
/// Uploads/downloads tenant branding assets to a personal Terabox account using the same
/// unofficial endpoints the Terabox web app itself calls (precreate -&gt; upload chunk -&gt; create,
/// and filemetas -&gt; dlink to read). Terabox publishes no official API; this is based on
/// publicly known reverse-engineering of the web client and will likely need adjusting
/// (host, exact parameter names, cookie shape) against the real account before it can be trusted.
///
/// appsettings:
/// <code>
/// "Terabox": {
///   "BaseUrl": "https://www.1024terabox.com",
///   "AppId": "...",
///   "Ndus": "...",          // session cookie from a logged-in Terabox web session
///   "JsToken": "...",       // fallback only; a fresh one is scraped from the homepage per call
///   "BdsToken": "...",      // query-string token required by the create (finalize) call
///   "RemoteDirectory": "/CodeKids"
///   // Ndus/JsToken/BdsToken are all captured from the browser the same way: log into the
///   // Terabox website, open DevTools -> Network, reload, find the "getinfo" request, and
///   // read "ndus" off its Cookie request header and "jsToken"/"bdstoken" off its query string.
/// }
/// </code>
/// </summary>
public sealed class TeraboxStorage(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TeraboxStorage> logger) : ITeraboxStorage
{
    private readonly IConfigurationSection _section = configuration.GetSection("Terabox");

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_section["Ndus"]) && !string.IsNullOrWhiteSpace(_section["BaseUrl"]);

    public async Task UploadAsync(string relativePath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (!IsEnabled) throw new InvalidOperationException("Terabox storage is not configured.");

        var client = httpClientFactory.CreateClient(nameof(TeraboxStorage));
        var baseUrl = _section["BaseUrl"]!.TrimEnd('/');
        var appId = _section["AppId"] ?? "";
        var (jsToken, bdsToken) = await FetchTokensAsync(client, baseUrl, cancellationToken);
        var remotePath = ResolvePath(relativePath);
        var targetPath = remotePath[..(remotePath.LastIndexOf('/') + 1)];
        var fileName = Path.GetFileName(remotePath);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var blockMd5 = Convert.ToHexString(MD5.HashData(bytes)).ToLowerInvariant();
        var blockListJson = JsonSerializer.Serialize(new[] { blockMd5 });

        var precreate = await PostFormAsync(client,
            BuildApiUrl(baseUrl, appId, jsToken, "/api/precreate"),
            new Dictionary<string, string>
            {
                ["path"] = remotePath,
                ["target_path"] = targetPath,
                ["size"] = bytes.Length.ToString(CultureInfo.InvariantCulture),
                ["block_list"] = blockListJson,
                ["autoinit"] = "1",
                ["rtype"] = "1",
                ["local_mtime"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            }, cancellationToken);

        if (!precreate.RootElement.TryGetProperty("uploadid", out var uploadIdProp))
        {
            var errno = precreate.RootElement.TryGetProperty("errno", out var errnoProp) ? errnoProp.GetInt32() : (int?)null;
            logger.LogWarning("Terabox precreate did not return an uploadid for {Path} (errno {Errno}): {Body}", remotePath, errno, precreate.RootElement);
            throw new InvalidOperationException($"Terabox precreate failed (no uploadid returned, errno {errno}).");
        }

        var uploadId = uploadIdProp.GetString()!;

        // The main www host answers api/* calls but returns 405 for the chunk upload itself;
        // Terabox serves that on a dedicated regional host (observed working: c-jp.1024terabox.com).
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post,
            $"https://c-jp.1024terabox.com/rest/2.0/pcs/superfile2?method=upload&app_id={Uri.EscapeDataString(appId)}&channel=dubox&clienttype=0&web=1&path=%2F{Uri.EscapeDataString(fileName)}&uploadid={Uri.EscapeDataString(uploadId)}&uploadsign=0&partseq=0");
        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(fileContent, "file", fileName);
        uploadRequest.Content = form;
        using var uploadResponse = await client.SendAsync(uploadRequest, cancellationToken);
        uploadResponse.EnsureSuccessStatusCode();

        var createBody = new Dictionary<string, string>
        {
            ["path"] = remotePath,
            ["target_path"] = targetPath,
            ["size"] = bytes.Length.ToString(CultureInfo.InvariantCulture),
            ["uploadid"] = uploadId,
            ["block_list"] = blockListJson,
            ["rtype"] = "1",
            ["isdir"] = "0"
        };
        if (!string.IsNullOrWhiteSpace(bdsToken)) createBody["bdstoken"] = bdsToken;

        var create = await PostFormAsync(client, BuildApiUrl(baseUrl, appId, jsToken, "/api/create"), createBody, cancellationToken);

        if (create.RootElement.TryGetProperty("errno", out var createErrno) && createErrno.GetInt32() != 0)
        {
            logger.LogWarning("Terabox create returned errno {Errno} for {Path}: {Body}", createErrno.GetInt32(), remotePath, create.RootElement);
            throw new InvalidOperationException($"Terabox upload failed (errno {createErrno.GetInt32()}).");
        }
    }

    public async Task<TeraboxFile?> DownloadAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (!IsEnabled) return null;

        var client = httpClientFactory.CreateClient(nameof(TeraboxStorage));
        var baseUrl = _section["BaseUrl"]!.TrimEnd('/');
        var appId = _section["AppId"] ?? "";
        var (jsToken, _) = await FetchTokensAsync(client, baseUrl, cancellationToken);
        var remotePath = ResolvePath(relativePath);
        var target = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { remotePath }));

        using var metaResponse = await client.GetAsync(
            $"{BuildApiUrl(baseUrl, appId, jsToken, "/api/filemetas")}&dlink=1&target={target}", cancellationToken);
        if (!metaResponse.IsSuccessStatusCode) return null;

        using var metaDoc = await JsonDocument.ParseAsync(await metaResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!metaDoc.RootElement.TryGetProperty("info", out var info) || info.GetArrayLength() == 0) return null;

        var dlink = info[0].TryGetProperty("dlink", out var dlinkProp) ? dlinkProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(dlink)) return null;

        var response = await client.GetAsync(dlink, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        return new TeraboxFile(stream, contentType);
    }

    private static string BuildApiUrl(string baseUrl, string appId, string jsToken, string path) =>
        $"{baseUrl}{path}?app_id={Uri.EscapeDataString(appId)}&web=1&channel=dubox&clienttype=0&jsToken={Uri.EscapeDataString(jsToken)}";

    /// <summary>
    /// The jsToken configured in appsettings goes stale quickly (errno 4000023, "jsToken
    /// invalid/expired" on write calls). The Terabox web client itself regenerates it from a
    /// `window.jsToken = "..."` assignment embedded in the logged-in homepage HTML, so we do
    /// the same on every call instead of relying on a value someone pasted in once. bdstoken is
    /// scraped the same way, best-effort.
    /// </summary>
    private async Task<(string JsToken, string BdsToken)> FetchTokensAsync(HttpClient client, string baseUrl, CancellationToken cancellationToken)
    {
        var configuredJsToken = _section["JsToken"] ?? "";
        var configuredBdsToken = _section["BdsToken"] ?? "";
        try
        {
            using var response = await client.GetAsync(baseUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return (configuredJsToken, configuredBdsToken);

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return (ExtractJsToken(html) ?? configuredJsToken, ExtractBdsToken(html) ?? configuredBdsToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Terabox token refresh failed, falling back to the configured values.");
            return (configuredJsToken, configuredBdsToken);
        }
    }

    private static string? ExtractJsToken(string html)
    {
        // The homepage embeds this inside `eval(decodeURIComponent(\`...\`))`, so the marker
        // itself is URL-encoded in the raw HTML, not the plain JS text it decodes to.
        const string marker = "fn%28%22";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start >= 0)
        {
            start += marker.Length;
            var end = html.IndexOf("%22%29", start, StringComparison.Ordinal);
            if (end > start) return html[start..end];
        }

        var match = Regex.Match(html, @"fn\(""([A-Fa-f0-9]+)""\)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractBdsToken(string html)
    {
        var match = Regex.Match(html, @"bdstoken""\s*:\s*""([^""]+)""");
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(html, @"bdstoken%22%3A%22([^%""']+)%22");
        return match.Success ? match.Groups[1].Value : null;
    }

    private string ResolvePath(string relativePath)
    {
        var root = (_section["RemoteDirectory"] ?? "/").TrimEnd('/');
        return $"{root}/{relativePath.TrimStart('/')}";
    }

    private static async Task<JsonDocument> PostFormAsync(HttpClient client, string url, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
    }
}
