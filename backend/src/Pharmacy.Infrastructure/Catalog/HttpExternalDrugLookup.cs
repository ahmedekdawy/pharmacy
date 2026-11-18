using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Infrastructure.Catalog;

/// <summary>
/// Generic, configuration-driven barcode lookup against any JSON HTTP API.
///
/// appsettings:
/// <code>
/// "DrugLookup": {
///   "External": {
///     "UrlTemplate": "https://provider.example/api/drugs?gtin={barcode}",
///     "ApiKeyHeader": "X-Api-Key",
///     "ApiKey": "...",                       // keep in user-secrets / env vars
///     "Fields": {                             // dotted JSON paths in the response
///       "NameEn": "data.tradeNameEn",
///       "NameAr": "data.tradeNameAr",
///       "ScientificName": "data.genericName",
///       "Manufacturer": "data.company",
///       "Price": "data.price",
///       "StripsPerBox": "data.strips",
///       "UnitsPerStrip": "data.unitsPerStrip"
///     }
///   }
/// }
/// </code>
/// When UrlTemplate is empty the provider is disabled. This lets the system plug into an official
/// EDA / GS1 Egypt service (or a commercial one) as soon as it is available, without code changes.
/// </summary>
public sealed class HttpExternalDrugLookup(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HttpExternalDrugLookup> logger) : IExternalDrugLookup
{
    private readonly IConfigurationSection _section = configuration.GetSection("DrugLookup:External");

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_section["UrlTemplate"]);

    public async Task<ExternalDrugInfo?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken)
    {
        if (!IsEnabled) return null;

        var url = _section["UrlTemplate"]!.Replace("{barcode}", Uri.EscapeDataString(barcode), StringComparison.Ordinal);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var apiKey = _section["ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
            request.Headers.TryAddWithoutValidation(_section["ApiKeyHeader"] ?? "X-Api-Key", apiKey);

        var client = httpClientFactory.CreateClient(nameof(HttpExternalDrugLookup));
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("External drug lookup returned {Status} for {Barcode}", (int)response.StatusCode, barcode);
            return null;
        }

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (root.GetArrayLength() == 0) return null;
            root = root[0];
        }

        var fields = _section.GetSection("Fields");
        string? Str(string key, string fallback) => Read(root, fields[key] ?? fallback);
        decimal? Dec(string key, string fallback) =>
            decimal.TryParse(Str(key, fallback), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
        int? Int(string key, string fallback) =>
            int.TryParse(Str(key, fallback), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;

        return new ExternalDrugInfo(
            Str("NameEn", "nameEn"),
            Str("NameAr", "nameAr"),
            Str("ScientificName", "scientificName"),
            Str("Manufacturer", "manufacturer"),
            Dec("Price", "price"),
            Int("StripsPerBox", "stripsPerBox"),
            Int("UnitsPerStrip", "unitsPerStrip"));
    }

    private static string? Read(JsonElement element, string path)
    {
        var current = element;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Array && int.TryParse(part, out var index))
            {
                if (index >= current.GetArrayLength()) return null;
                current = current[index];
                continue;
            }

            if (current.ValueKind != JsonValueKind.Object) return null;
            var found = false;
            foreach (var prop in current.EnumerateObject())
            {
                if (string.Equals(prop.Name, part, StringComparison.OrdinalIgnoreCase))
                {
                    current = prop.Value;
                    found = true;
                    break;
                }
            }

            if (!found) return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString()?.Trim(),
            JsonValueKind.Number => current.GetRawText(),
            _ => null
        };
    }
}
