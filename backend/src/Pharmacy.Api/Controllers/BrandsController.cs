using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
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
    {
        var result = await mediator.Send(new GetBrandsQuery(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.BrandManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateBrandCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
