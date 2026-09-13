using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Locations.Commands.CreateLocation;
using Pharmacy.Application.Locations.Queries.GetLocations;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/locations")]
public sealed class LocationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.LocationView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLocationsQuery(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.LocationManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateLocationCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
