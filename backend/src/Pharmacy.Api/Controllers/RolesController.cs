using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Identity.Roles.Commands;
using Pharmacy.Application.Identity.Roles.Commands.CreateRole;
using Pharmacy.Application.Identity.Roles.Queries.GetRoles;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/roles")]
public sealed class RolesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.RoleView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetRolesQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.RoleManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateRoleCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.RoleManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] RoleUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateRoleCommand(
            id,
            body.Code,
            body.NameEn,
            body.NameAr,
            body.PermissionIds,
            body.PageIds,
            body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.RoleManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteRoleCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record RoleUpdateRequest(
    string Code,
    string NameEn,
    string NameAr,
    IReadOnlyList<Guid> PermissionIds,
    IReadOnlyList<Guid> PageIds,
    bool IsActive = true);
