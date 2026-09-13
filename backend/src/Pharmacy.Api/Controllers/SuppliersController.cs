using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Suppliers;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/suppliers")]
public sealed class SuppliersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.SupplierView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetSuppliersQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.SupplierManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateSupplierCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));
}
