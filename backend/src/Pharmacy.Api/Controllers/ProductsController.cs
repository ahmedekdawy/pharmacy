using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Catalog.Products.Commands.CreateProduct;
using Pharmacy.Application.Catalog.Products.Commands.DeleteProduct;
using Pharmacy.Application.Catalog.Products.Commands.UpdateProduct;
using Pharmacy.Application.Catalog.Products.Queries.SearchProducts;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/products")]
public sealed class ProductsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ProductView)]
    public async Task<ActionResult<ApiResponse<object>>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new SearchProductsQuery(search, page, pageSize), cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.ProductCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.ProductEdit)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        Guid id,
        [FromBody] UpdateProductRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateProductCommand(
            id,
            body.Code,
            body.NameAr,
            body.NameEn,
            body.Barcode,
            body.CategoryId,
            body.BrandId,
            body.SellingPrice,
            body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.ProductDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteProductCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record UpdateProductRequest(
    string Code,
    string NameAr,
    string NameEn,
    string? Barcode,
    Guid? CategoryId,
    Guid? BrandId,
    decimal SellingPrice,
    bool IsActive = true);
