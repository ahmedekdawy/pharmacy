using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Inventory.Commands;
using Pharmacy.Application.Inventory.Commands.CreateOpeningBalance;
using Pharmacy.Application.Inventory.Queries;
using Pharmacy.Application.Inventory.Queries.GetNearExpiry;
using Pharmacy.Application.Inventory.Queries.GetStock;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/inventory")]
public sealed class InventoryController(IMediator mediator) : ControllerBase
{
    [HttpGet("stock")]
    [RequirePermission(PermissionCodes.InventoryView)]
    public async Task<ActionResult<ApiResponse<object>>> GetStock(
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? productId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetStockQuery(locationId, productId, search), cancellationToken)));

    [HttpGet("near-expiry")]
    [RequirePermission(PermissionCodes.InventoryView)]
    public async Task<ActionResult<ApiResponse<object>>> GetNearExpiry(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetNearExpiryQuery(days), cancellationToken)));

    [HttpGet("low-stock")]
    [RequirePermission(PermissionCodes.InventoryView)]
    public async Task<ActionResult<ApiResponse<object>>> GetLowStock(
        [FromQuery] decimal threshold = 10,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetLowStockQuery(threshold), cancellationToken)));

    [HttpPost("opening-balance")]
    [RequirePermission(PermissionCodes.InventoryAdjust)]
    public async Task<ActionResult<ApiResponse<object>>> OpeningBalance(
        [FromBody] CreateOpeningBalanceCommand command,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPost("transfer")]
    [RequirePermission(PermissionCodes.InventoryAdjust)]
    public async Task<ActionResult<ApiResponse<object>>> Transfer(
        [FromBody] TransferStockCommand command,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPost("adjust")]
    [RequirePermission(PermissionCodes.InventoryAdjust)]
    public async Task<ActionResult<ApiResponse<object>>> Adjust(
        [FromBody] AdjustInventoryCommand command,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));
}
