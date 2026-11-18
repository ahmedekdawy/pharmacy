using MediatR;

namespace Pharmacy.Application.Catalog.Products.Commands.CreateProduct;

/// <param name="SellingPrice">Price of one box.</param>
/// <param name="StripsPerBox">Strips (شرائط) in a box.</param>
/// <param name="UnitsPerStrip">Pills/units (حبات) in a strip.</param>
public sealed record CreateProductCommand(
    string Code,
    string NameAr,
    string NameEn,
    string? Barcode,
    Guid? CategoryId,
    Guid? BrandId,
    decimal SellingPrice,
    int StripsPerBox = 1,
    int UnitsPerStrip = 1,
    bool AllowStripSale = false,
    bool AllowUnitSale = false,
    decimal? StripPrice = null,
    decimal? UnitPrice = null,
    Guid? Id = null,
    string? ScientificName = null,
    string? Manufacturer = null) : IRequest<Guid>;
