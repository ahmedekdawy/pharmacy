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
    public async Task<ActionResult<ApiResponse<object>>> Get([FromQuery] bool onlyDebtors, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetCustomersQuery(onlyDebtors), cancellationToken)));

    [HttpGet("{id:guid}/statement")]
    [RequirePermission(PermissionCodes.CustomerView)]
    public async Task<ActionResult<ApiResponse<object>>> Statement(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetCustomerStatementQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/payments")]
    [RequirePermission(PermissionCodes.CustomerPayment)]
    public async Task<ActionResult<ApiResponse<object>>> Pay(Guid id, [FromBody] CustomerPaymentRequest body, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(
            new CreateCustomerPaymentCommand(id, body.Amount, body.PaymentMethod, body.LocationId, body.Notes, body.Id),
            cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateCustomerCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CustomerManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] CustomerUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCustomerCommand(id, body.Code, body.NameEn, body.NameAr, body.Phone, body.IsActive, body.Address, body.CreditLimit), cancellationToken);
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

public sealed record CustomerUpdateRequest(
    string Code,
    string NameEn,
    string NameAr,
    string? Phone,
    bool IsActive = true,
    string? Address = null,
    decimal CreditLimit = 0);

public sealed record CustomerPaymentRequest(
    decimal Amount,
    string? PaymentMethod,
    Guid? LocationId,
    string? Notes,
    Guid? Id);
