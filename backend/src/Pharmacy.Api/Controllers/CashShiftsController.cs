using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.CashManagement;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cash-shifts")]
public sealed class CashShiftsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.CashShiftView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetCashShiftsQuery(), cancellationToken)));

    [HttpPost("open")]
    [RequirePermission(PermissionCodes.CashShiftOpen)]
    public async Task<ActionResult<ApiResponse<object>>> Open(
        [FromBody] OpenCashShiftCommand command,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCodes.CashShiftClose)]
    public async Task<ActionResult<ApiResponse<object>>> Close(
        Guid id,
        [FromBody] CloseCashShiftRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CloseCashShiftCommand(id, body.ClosingCash, body.Notes), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record CloseCashShiftRequest(decimal ClosingCash, string? Notes);
