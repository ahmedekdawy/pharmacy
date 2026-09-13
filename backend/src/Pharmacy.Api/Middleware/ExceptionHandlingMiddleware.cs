using System.Net;
using System.Text.Json;
using FluentValidation;
using Pharmacy.Shared.Results;

namespace Pharmacy.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, ex.Message);
        }
        catch (ValidationException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Errors.Select(e => e.ErrorMessage).ToArray());
        }
        catch (InvalidOperationException ex)
        {
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "UnexpectedError");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, params string[] errors)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        var payload = ApiResponse<object>.Fail(errors);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
