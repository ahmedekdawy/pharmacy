using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Branding;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/tenants")]
public sealed class BrandingController(IMediator mediator) : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    [Authorize]
    [RequirePermission(PermissionCodes.SettingsManage)]
    [HttpPost("branding/{kind}")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<ApiResponse<object>>> Upload(
        BrandingKind kind,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File is required."));
        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResponse<object>.Fail("File is too large."));

        await using var stream = file.OpenReadStream();
        var path = await mediator.Send(
            new UploadTenantBrandingCommand(kind, file.FileName, file.ContentType, stream),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { path }));
    }

    [AllowAnonymous]
    [HttpGet("{code}/branding/{kind}")]
    public async Task<IActionResult> Get(string code, BrandingKind kind, CancellationToken cancellationToken)
    {
        var file = await mediator.Send(new GetTenantBrandingQuery(code, kind), cancellationToken);
        if (file is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=3600";
        return File(file.Content, file.ContentType);
    }
}
