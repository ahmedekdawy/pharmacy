using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Pharmacy.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var hasPermission = user.Claims.Any(c =>
            c.Type == "permission" &&
            string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}
