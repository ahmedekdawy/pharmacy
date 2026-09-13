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
[Route("v{version:apiVersion}/suppliers")]
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

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.SupplierManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] SupplierUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSupplierCommand(id, body.Code, body.NameEn, body.NameAr, body.Phone, body.Email, body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.SupplierManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteSupplierCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record SupplierUpdateRequest(string Code, string NameEn, string NameAr, string? Phone, string? Email, bool IsActive = true);
