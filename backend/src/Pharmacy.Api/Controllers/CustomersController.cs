using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Customers;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/customers")]
public sealed class CustomersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.CustomerView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetCustomersQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateCustomerCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] CustomerUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCustomerCommand(id, body.Code, body.NameEn, body.NameAr, body.Phone, body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCustomerCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record CustomerUpdateRequest(string Code, string NameEn, string NameAr, string? Phone, bool IsActive = true);
