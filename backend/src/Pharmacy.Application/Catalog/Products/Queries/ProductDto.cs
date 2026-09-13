namespace Pharmacy.Application.Catalog.Products.Queries;

public sealed record ProductDto(
    Guid Id,
    string Code,
    string NameAr,
    string NameEn,
    string? Barcode,
    Guid? CategoryId,
    Guid? BrandId,
    decimal SellingPrice,
    bool IsActive);
