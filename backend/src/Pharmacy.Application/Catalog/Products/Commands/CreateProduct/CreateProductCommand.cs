using MediatR;

namespace Pharmacy.Application.Catalog.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Code,
    string NameAr,
    string NameEn,
    string? Barcode,
    Guid? CategoryId,
    Guid? BrandId,
    decimal SellingPrice) : IRequest<Guid>;
