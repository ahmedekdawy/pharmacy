using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Sales;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/sales")]
public sealed class SalesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.SaleView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? customerId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetSalesQuery(from, to, customerId, take), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.SaleView)]
    public async Task<ActionResult<ApiResponse<object>>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetSaleDetailQuery(id), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.SaleCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateSaleCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(command with { IsOffline = false }, cancellationToken)));
}
