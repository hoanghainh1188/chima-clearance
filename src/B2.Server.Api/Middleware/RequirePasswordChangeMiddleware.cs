using System.Text.Json;

namespace B2.Server.Api.Middleware;

/// <summary>
/// FR-017: when an authenticated user carries <c>must_change_password=true</c>,
/// every request except the change-password and logout endpoints is rejected
/// with HTTP 403 / <c>PASSWORD_CHANGE_REQUIRED</c>.
/// Anonymous requests pass through untouched.
/// </summary>
public sealed class RequirePasswordChangeMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPaths =
    {
        "/api/auth/change-password",
        "/api/auth/logout",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            string.Equals(context.User.FindFirst("must_change_password")?.Value, "true", StringComparison.Ordinal) &&
            !IsAllowed(context.Request))
        {
            await WriteForbiddenAsync(context);
            return;
        }

        await next(context);
    }

    private static bool IsAllowed(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        foreach (var allowed in AllowedPaths)
        {
            if (request.Path.Equals(allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task WriteForbiddenAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? string.Empty;
        var payload = JsonSerializer.Serialize(new
        {
            type = "https://b2/errors/password-change-required",
            title = "PASSWORD_CHANGE_REQUIRED",
            status = StatusCodes.Status403Forbidden,
            detail = "User must change password before accessing this resource.",
            code = "PASSWORD_CHANGE_REQUIRED",
            correlationId,
        });
        await context.Response.WriteAsync(payload);
    }
}
