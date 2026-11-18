using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pharmacy.Application.Catalog.Products.Queries;
using Pharmacy.Application.Common.Interfaces;
using Pharmacy.Domain.Catalog;

namespace Pharmacy.Application.Catalog.Barcodes;

public sealed record BarcodeSuggestionDto(
    string NameEn,
    string NameAr,
    string? ScientificName,
    string? Manufacturer,
    decimal? Price,
    int StripsPerBox,
    int UnitsPerStrip,
    string Source,
    int Confirmations);

public sealed record BarcodeLookupDto(
    string Raw,
    string Barcode,
    string? Gtin,
    DateOnly? Expiry,
    string? Batch,
    string? Serial,
    /// <summary>Product | Registry | External | Catalog | None</summary>
    string Source,
    ProductDto? Product,
    BarcodeSuggestionDto? Suggestion,
    IReadOnlyList<EgyptianDrugDto> CatalogMatches);

/// <summary>
/// Barcode → product. Order: this pharmacy's products → shared registry (barcodes saved by any
/// pharmacy) → configured external API. Also decodes GS1 DataMatrix (GTIN, expiry, batch).
/// </summary>
public sealed record LookupBarcodeQuery(string Code) : IRequest<BarcodeLookupDto>;

public sealed class LookupBarcodeQueryHandler(
    IPharmacyDbContext db,
    ICurrentTenant currentTenant,
    IExternalDrugLookup external,
    IEgyptianDrugCatalog catalog,
    ILogger<LookupBarcodeQueryHandler> logger) : IRequestHandler<LookupBarcodeQuery, BarcodeLookupDto>
{
    public async Task<BarcodeLookupDto> Handle(LookupBarcodeQuery request, CancellationToken cancellationToken)
    {
        if (!currentTenant.IsResolved) throw new InvalidOperationException("Tenant is required.");

        var gs1 = Gs1Parser.Parse(request.Code);
        var barcode = Gs1Parser.Normalize(request.Code);
        var candidates = Gs1Parser.Candidates(request.Code);

        BarcodeLookupDto Result(string source, ProductDto? product, BarcodeSuggestionDto? suggestion, IReadOnlyList<EgyptianDrugDto>? matches = null) =>
            new(request.Code, barcode, gs1.Gtin, gs1.Expiry, gs1.Batch, gs1.Serial, source, product, suggestion, matches ?? []);

        if (string.IsNullOrWhiteSpace(barcode)) return Result("None", null, null);

        // 1) Already in this pharmacy.
        var product = await db.Products.AsNoTracking()
            .Where(x => x.Barcode != null && candidates.Contains(x.Barcode))
            .Select(x => new ProductDto(x.Id, x.Code, x.NameAr, x.NameEn, x.Barcode, x.CategoryId, x.BrandId,
                x.SellingPrice, x.IsActive, x.StripsPerBox, x.UnitsPerStrip, x.AllowStripSale, x.AllowUnitSale,
                x.StripPrice, x.UnitPrice))
            .FirstOrDefaultAsync(cancellationToken);
        if (product is not null) return Result("Product", product, null);

        // 2) Shared registry.
        var known = await db.DrugBarcodes.AsNoTracking()
            .Where(x => candidates.Contains(x.Barcode))
            .OrderByDescending(x => x.Confirmations)
            .FirstOrDefaultAsync(cancellationToken);
        if (known is not null)
        {
            var suggestion = new BarcodeSuggestionDto(known.NameEn, known.NameAr, known.ScientificName, known.Manufacturer,
                known.PublicPrice, known.StripsPerBox, known.UnitsPerStrip, known.Source, known.Confirmations);
            return Result("Registry", null, suggestion, await CatalogMatchesAsync(known.NameEn, cancellationToken));
        }

        // 3) External provider (when configured).
        if (external.IsEnabled)
        {
            try
            {
                var info = await external.FindByBarcodeAsync(barcode, cancellationToken);
                if (info is not null && (!string.IsNullOrWhiteSpace(info.NameEn) || !string.IsNullOrWhiteSpace(info.NameAr)))
                {
                    var nameEn = info.NameEn ?? info.NameAr!;
                    var nameAr = info.NameAr ?? info.NameEn!;
                    db.DrugBarcodes.Add(new DrugBarcode
                    {
                        Barcode = barcode,
                        NameEn = Trim(nameEn, 256),
                        NameAr = Trim(nameAr, 256),
                        ScientificName = info.ScientificName,
                        Manufacturer = info.Manufacturer,
                        PublicPrice = info.Price,
                        StripsPerBox = Math.Max(1, info.StripsPerBox ?? 1),
                        UnitsPerStrip = Math.Max(1, info.UnitsPerStrip ?? 1),
                        Source = "External",
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                    await db.SaveChangesAsync(cancellationToken);

                    var suggestion = new BarcodeSuggestionDto(nameEn, nameAr, info.ScientificName, info.Manufacturer, info.Price,
                        Math.Max(1, info.StripsPerBox ?? 1), Math.Max(1, info.UnitsPerStrip ?? 1), "External", 1);
                    return Result("External", null, suggestion, await CatalogMatchesAsync(nameEn, cancellationToken));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ClearChanges();
                logger.LogWarning(ex, "External drug lookup failed for {Barcode}", barcode);
            }
        }

        return Result("None", null, null);
    }

    private async Task<IReadOnlyList<EgyptianDrugDto>> CatalogMatchesAsync(string name, CancellationToken ct)
    {
        try
        {
            var firstWord = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? name;
            return await catalog.SearchAsync(firstWord, 5, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static string Trim(string value, int max) => value.Length > max ? value[..max] : value;
}

/// <summary>Keeps the shared barcode registry up to date when a pharmacy saves a product.</summary>
public static class DrugBarcodeRegistry
{
    public static async Task RememberAsync(IPharmacyDbContext db, Product product, string? scientificName, string? manufacturer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(product.Barcode)) return;
        var barcode = Gs1Parser.Normalize(product.Barcode);
        if (barcode.Length < 6) return; // internal short codes are not shared

        var entry = await db.DrugBarcodes.FirstOrDefaultAsync(x => x.Barcode == barcode, ct);
        if (entry is null)
        {
            db.DrugBarcodes.Add(new DrugBarcode
            {
                Barcode = barcode,
                NameEn = product.NameEn,
                NameAr = product.NameAr,
                ScientificName = scientificName,
                Manufacturer = manufacturer,
                StripsPerBox = product.StripsPerBox,
                UnitsPerStrip = product.UnitsPerStrip,
                PublicPrice = product.SellingPrice > 0 ? product.SellingPrice : null,
                Source = "Pharmacy",
                UpdatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        // Same identity confirmed by another save: trust it more and keep the latest public price.
        if (string.Equals(entry.NameEn, product.NameEn, StringComparison.OrdinalIgnoreCase))
        {
            entry.Confirmations++;
            if (product.SellingPrice > 0) entry.PublicPrice = product.SellingPrice;
            entry.StripsPerBox = product.StripsPerBox;
            entry.UnitsPerStrip = product.UnitsPerStrip;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else if (entry.Confirmations <= 1 && entry.Source == "Pharmacy")
        {
            // A single unconfirmed entry is replaced by the newer one.
            entry.NameEn = product.NameEn;
            entry.NameAr = product.NameAr;
            entry.PublicPrice = product.SellingPrice > 0 ? product.SellingPrice : entry.PublicPrice;
            entry.StripsPerBox = product.StripsPerBox;
            entry.UnitsPerStrip = product.UnitsPerStrip;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
