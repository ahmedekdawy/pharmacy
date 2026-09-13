using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Catalog.Brands.Commands;
using Pharmacy.Application.Catalog.Brands.Commands.CreateBrand;
using Pharmacy.Application.Catalog.Brands.Queries.GetBrands;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/brands")]
public sealed class BrandsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.BrandView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetBrandsQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.BrandManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateBrandCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.BrandManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] NamedUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateBrandCommand(id, body.NameEn, body.NameAr, body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.BrandManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteBrandCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
