using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Locations.Commands;
using Pharmacy.Application.Locations.Commands.CreateLocation;
using Pharmacy.Application.Locations.Queries.GetLocations;
using Pharmacy.Domain.Identity;
using Pharmacy.Domain.Locations;
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
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetLocationsQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.LocationManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateLocationCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.LocationManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] LocationUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateLocationCommand(id, body.NameEn, body.NameAr, body.Type, body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.LocationManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteLocationCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record LocationUpdateRequest(string NameEn, string NameAr, LocationType Type, bool IsActive = true);
