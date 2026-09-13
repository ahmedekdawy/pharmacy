using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Catalog.EgyptianDrugs;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/catalog/egyptian-drugs")]
public sealed class EgyptianDrugsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.ProductView)]
    public async Task<ActionResult<ApiResponse<object>>> Search(
        [FromQuery] string? search,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse<object>.Ok(
            await mediator.Send(new SearchEgyptianDrugsQuery(search, limit), cancellationToken)));
}
