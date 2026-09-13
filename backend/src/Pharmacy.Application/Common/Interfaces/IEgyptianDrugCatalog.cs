namespace Pharmacy.Application.Common.Interfaces;

public sealed record EgyptianDrugDto(
    string CommercialNameEn,
    string CommercialNameAr,
    string ScientificName,
    string Manufacturer,
    string DrugClass,
    string Route,
    decimal PriceEgp);

public interface IEgyptianDrugCatalog
{
    Task<IReadOnlyList<EgyptianDrugDto>> SearchAsync(string? search, int limit, CancellationToken cancellationToken = default);
}
