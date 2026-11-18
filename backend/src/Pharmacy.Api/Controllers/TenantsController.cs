using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Identity.Tenants.Queries.GetTenantPublicInfo;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/tenants")]
public sealed class TenantsController(IMediator mediator) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("{code}/public-info")]
    public async Task<ActionResult<ApiResponse<TenantPublicInfoDto>>> GetPublicInfo(
        string code,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetTenantPublicInfoQuery(code), cancellationToken);
        return Ok(ApiResponse<TenantPublicInfoDto>.Ok(result));
    }
}
