using Pharmacy.Infrastructure.Identity;
using Pharmacy.Infrastructure.Tenancy;

namespace Pharmacy.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, CurrentTenant currentTenant, CurrentUser currentUser)
    {
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var claimTenantId))
        {
            currentTenant.SetTenant(claimTenantId);
        }
        else if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeader) &&
                 Guid.TryParse(tenantHeader.FirstOrDefault(), out var headerTenantId))
        {
            currentTenant.SetTenant(headerTenantId);
        }

        if (context.User.Identity?.IsAuthenticated == true &&
            context.Request.Headers.TryGetValue(TenantHeaderName, out var supplied) &&
            Guid.TryParse(supplied.FirstOrDefault(), out var suppliedTenant) &&
            currentTenant.TenantId is Guid resolved &&
            suppliedTenant != resolved)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Tenant header does not match token tenant.");
            return;
        }

        var userClaim = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst("user_id")?.Value;
        if (Guid.TryParse(userClaim, out var userId))
        {
            currentUser.SetUser(userId);
        }

        await next(context);
    }
}
