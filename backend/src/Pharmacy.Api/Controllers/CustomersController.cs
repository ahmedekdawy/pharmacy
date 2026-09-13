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
[Route("api/v{version:apiVersion}/customers")]
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
}
