using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.Catalog.Categories.Commands;
using Pharmacy.Application.Catalog.Categories.Commands.CreateCategory;
using Pharmacy.Application.Catalog.Categories.Queries.GetCategories;
using Pharmacy.Domain.Identity;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categories")]
public sealed class CategoriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.CategoryView)]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(await mediator.Send(new GetCategoriesQuery(), cancellationToken)));

    [HttpPost]
    [RequirePermission(PermissionCodes.CategoryManage)]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreateCategoryCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<object>.Ok(new { id = await mediator.Send(command, cancellationToken) }));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CategoryManage)]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, [FromBody] NamedUpdateRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCategoryCommand(id, body.NameEn, body.NameAr, body.IsActive), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CategoryManage)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCategoryCommand(id), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id }));
    }
}

public sealed record NamedUpdateRequest(string NameEn, string NameAr, bool IsActive = true);
