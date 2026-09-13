using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Inventory.Commands.CreateOpeningBalance;
using Pharmacy.Application.Inventory.Queries.GetNearExpiry;
using Pharmacy.Application.Inventory.Queries.GetStock;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/inventory")]
public sealed class InventoryController(IMediator mediator) : ControllerBase
{
    [HttpGet("stock")]
    [RequirePermission(PermissionCodes.InventoryView)]
    public async Task<ActionResult<ApiResponse<object>>> GetStock(
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? productId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStockQuery(locationId, productId, search), cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("near-expiry")]
    [RequirePermission(PermissionCodes.InventoryView)]
    public async Task<ActionResult<ApiResponse<object>>> GetNearExpiry(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetNearExpiryQuery(days), cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost("opening-balance")]
    [RequirePermission(PermissionCodes.InventoryAdjust)]
    public async Task<ActionResult<ApiResponse<object>>> OpeningBalance(
        [FromBody] CreateOpeningBalanceCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}
