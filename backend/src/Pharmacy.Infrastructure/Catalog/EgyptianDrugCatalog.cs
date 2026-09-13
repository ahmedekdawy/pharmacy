using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Pharmacy.Application.Common.Interfaces;

namespace Pharmacy.Infrastructure.Catalog;

public sealed class EgyptianDrugCatalog(IHttpClientFactory httpClientFactory, ILogger<EgyptianDrugCatalog> logger)
    : IEgyptianDrugCatalog
{
    public const string SourceUrl =
        "https://raw.githubusercontent.com/karem505/egyptian-drug-database/main/data/egyptian-drugs.json";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<EgyptianDrugDto>? _cache;

    public async Task<IReadOnlyList<EgyptianDrugDto>> SearchAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var drugs = await EnsureLoadedAsync(cancellationToken);
        var take = Math.Clamp(limit, 1, 50);
        var term = search?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(term))
        {
            return drugs.Take(take).ToList();
        }

        return drugs
            .Where(d => Matches(d, term))
            .Take(take)
            .ToList();
    }

    private async Task<IReadOnlyList<EgyptianDrugDto>> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var client = httpClientFactory.CreateClient(nameof(EgyptianDrugCatalog));
            var payload = await client.GetFromJsonAsync<List<EgyptianDrugJson>>(SourceUrl, cancellationToken)
                ?? [];

            _cache = payload
                .Select(x => new EgyptianDrugDto(
                    x.CommercialNameEn?.Trim() ?? string.Empty,
                    x.CommercialNameAr?.Trim() ?? string.Empty,
                    x.ScientificName?.Trim() ?? string.Empty,
                    x.Manufacturer?.Trim() ?? string.Empty,
                    x.DrugClass?.Trim() ?? string.Empty,
                    x.Route?.Trim() ?? string.Empty,
                    x.PriceEgp))
                .Where(x => !string.IsNullOrWhiteSpace(x.CommercialNameEn) || !string.IsNullOrWhiteSpace(x.CommercialNameAr))
                .ToList();

            logger.LogInformation("Loaded {Count} Egyptian drugs from catalog", _cache.Count);
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool Matches(EgyptianDrugDto drug, string term)
    {
        return Contains(drug.CommercialNameEn, term)
            || Contains(drug.CommercialNameAr, term)
            || Contains(drug.ScientificName, term)
            || Contains(drug.Manufacturer, term)
            || Contains(drug.DrugClass, term);
    }

    private static bool Contains(string value, string term) =>
        !string.IsNullOrEmpty(value) &&
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private sealed class EgyptianDrugJson
    {
        [JsonPropertyName("commercial_name_en")]
        public string? CommercialNameEn { get; set; }

        [JsonPropertyName("commercial_name_ar")]
        public string? CommercialNameAr { get; set; }

        [JsonPropertyName("scientific_name")]
        public string? ScientificName { get; set; }

        [JsonPropertyName("manufacturer")]
        public string? Manufacturer { get; set; }

        [JsonPropertyName("drug_class")]
        public string? DrugClass { get; set; }

        [JsonPropertyName("route")]
        public string? Route { get; set; }

        [JsonPropertyName("price_egp")]
        public decimal PriceEgp { get; set; }
    }
}
