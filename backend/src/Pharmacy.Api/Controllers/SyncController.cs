using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Sync;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

/// <summary>Offline support: devices download a snapshot and upload operations recorded while offline.</summary>
[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/sync")]
public sealed class SyncController(IMediator mediator) : ControllerBase
{
    [HttpGet("snapshot")]
    [RequirePermission(PermissionCodes.SaleView)]
    public async Task<ActionResult<ApiResponse<object>>> Snapshot(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetSyncSnapshotQuery(), cancellationToken)));

    [HttpPost("push")]
    [RequirePermission(PermissionCodes.SaleView)]
    public async Task<ActionResult<ApiResponse<object>>> Push([FromBody] SyncPushRequest body, CancellationToken cancellationToken)
    {
        var permissions = User.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        var result = await mediator.Send(
            new PushSyncCommand(body.Operations ?? [], body.DeviceId, permissions),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { results = result, serverTime = DateTimeOffset.UtcNow }));
    }
}

public sealed record SyncPushRequest(string? DeviceId, IReadOnlyList<SyncOperationInput>? Operations);
