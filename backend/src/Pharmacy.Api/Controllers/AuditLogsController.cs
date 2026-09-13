using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Audit;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/audit-logs")]
public sealed class AuditLogsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.AuditView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetAuditLogsQuery(take), cancellationToken)));
}
