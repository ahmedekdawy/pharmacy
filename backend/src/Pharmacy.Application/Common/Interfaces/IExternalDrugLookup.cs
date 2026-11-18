namespace Pharmacy.Application.Common.Interfaces;

public sealed record ExternalDrugInfo(
    string? NameEn,
    string? NameAr,
    string? ScientificName,
    string? Manufacturer,
    decimal? Price,
    int? StripsPerBox,
    int? UnitsPerStrip);

/// <summary>
/// Optional third-party barcode → drug lookup (e.g. EDA / GS1 Egypt once they publish an API,
/// or a commercial provider). Disabled when not configured.
/// </summary>
public interface IExternalDrugLookup
{
    bool IsEnabled { get; }
    Task<ExternalDrugInfo?> FindByBarcodeAsync(string barcode, CancellationToken cancellationToken);
}
