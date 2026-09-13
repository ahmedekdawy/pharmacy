using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Identity.Users.Commands.CreateUser;
using Pharmacy.Application.Identity.Users.Commands.DeleteUser;
using Pharmacy.Application.Identity.Users.Commands.SetUserActive;
using Pharmacy.Application.Identity.Users.Commands.UpdateUser;
using Pharmacy.Application.Identity.Users.Queries.SearchUsers;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.UserView)]
    public async Task<ActionResult<ApiResponse<object>>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new SearchUsersQuery(search, page, pageSize), cancellationToken);
        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.UserCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.UserEdit)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        Guid id,
        [FromBody] UpdateUserRequest body,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateUserCommand(
            id,
            body.Email,
            body.FullNameEn,
            body.FullNameAr,
            body.Password,
            body.RoleIds,
            body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.UserEdit)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUserCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(PermissionCodes.UserActivate)]
    public async Task<ActionResult<ApiResponse<object>>> Activate(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetUserActiveCommand(id, true), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id, isActive = true }));
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(PermissionCodes.UserActivate)]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetUserActiveCommand(id, false), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id, isActive = false }));
    }
}

public sealed record UpdateUserRequest(
    string Email,
    string FullNameEn,
    string FullNameAr,
    string? Password,
    IReadOnlyList<Guid>? RoleIds,
    bool IsActive);
