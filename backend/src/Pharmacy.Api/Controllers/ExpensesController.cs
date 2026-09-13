using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Expenses;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/expenses")]
public sealed class ExpensesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ExpenseView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetExpensesQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.ExpenseManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateExpenseCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.ExpenseManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] ExpenseUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateExpenseCommand(id, body.LocationId, body.Category, body.DescriptionEn, body.DescriptionAr, body.Amount), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.ExpenseManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteExpenseCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record ExpenseUpdateRequest(
    Guid LocationId,
    string Category,
    string DescriptionEn,
    string DescriptionAr,
    decimal Amount);
