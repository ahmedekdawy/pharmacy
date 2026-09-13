using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Settings;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/settings")]
public sealed class SettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.SettingsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetTenantSettingsQuery(), cancellationToken)));

    [HttpPut]
    [RequirePermission(PermissionCodes.SettingsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Upsert(
        [FromBody] UpsertTenantSettingCommand command,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { command.Key }));
    }
}
