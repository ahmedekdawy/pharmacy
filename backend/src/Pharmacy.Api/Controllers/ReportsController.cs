using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Reporting;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
public sealed class ReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("sales")]
    [RequirePermission(PermissionCodes.ReportSales)]
    public async Task<ActionResult<ApiResponse<object>>> Sales(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetSalesReportQuery(from, to), cancellationToken)));
}
