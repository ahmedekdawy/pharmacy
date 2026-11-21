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
[Route("v{version:apiVersion}/catalog/egyptian-drugs")]
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

    /// <summary>Bulk fetch for clients (POS) that cache the whole list to search it offline.</summary>
    [HttpGet("all")]
    [RequirePermission(PermissionCodes.ProductView)]
    public async Task<ActionResult<ApiResponse<object>>> All(CancellationToken cancellationToken = default)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetAllEgyptianDrugsQuery(), cancellationToken)));

    /// <summary>
    /// Look up a scanned code (EAN-13 or GS1 DataMatrix). Passed as a query string because
    /// DataMatrix content can include characters that are not valid in a URL path.
    /// </summary>
    [HttpGet("~/v{version:apiVersion}/catalog/barcode")]
    [RequirePermission(PermissionCodes.ProductView)]
    public async Task<ActionResult<ApiResponse<object>>> Barcode(
        [FromQuery] string code,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse<object>.Ok(
            await mediator.Send(new Pharmacy.Application.Catalog.Barcodes.LookupBarcodeQuery(code ?? string.Empty), cancellationToken)));
}
