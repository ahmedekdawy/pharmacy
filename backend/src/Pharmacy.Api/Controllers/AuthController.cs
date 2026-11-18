using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Application.Identity.Auth.Commands.Login;
using Pharmacy.Application.Identity.Tenants.Commands.RegisterTenant;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<LoginResult>.Ok(result));
    }

    [AllowAnonymous]
    [HttpPost("register-tenant")]
    public async Task<ActionResult<ApiResponse<LoginResult>>> RegisterTenant(
        [FromBody] RegisterTenantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(ApiResponse<LoginResult>.Ok(result));
    }
}
