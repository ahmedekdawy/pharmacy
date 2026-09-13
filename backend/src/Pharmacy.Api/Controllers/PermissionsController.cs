using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Identity.Permissions.Queries.GetPermissions;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/permissions")]
public sealed class PermissionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.PermissionView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPermissionsQuery(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }
}
