using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Inventory;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

/// <summary>Shortages list (النواقص): items to reorder or customer requests the pharmacy does not stock.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/shortages")]
public sealed class ShortagesController(IMediator mediator) : ControllerBase
{
    /// <param name="status">Open, Ordered, Received, Cancelled or Active (open + ordered).</param>
    [HttpGet]
    [RequirePermission(PermissionCodes.ShortageView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? locationId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetShortagesQuery(status, from, to, locationId, search), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.ShortageCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateShortageCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.ShortageManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] ShortageUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateShortageCommand(id, body.Quantity, body.Status, body.Notes, body.CustomerName, body.CustomerPhone), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.ShortageManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteShortageCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record ShortageUpdateRequest(
    decimal Quantity,
    string Status,
    string? Notes,
    string? CustomerName,
    string? CustomerPhone);
