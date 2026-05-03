using Serilog.Context;

namespace B2.Server.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string LegacyHeader = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;
        if (context.Request.Headers.TryGetValue(LegacyHeader, out var legacy) && !string.IsNullOrWhiteSpace(legacy))
        {
            correlationId = legacy.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers.TryAdd(LegacyHeader, correlationId);
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
