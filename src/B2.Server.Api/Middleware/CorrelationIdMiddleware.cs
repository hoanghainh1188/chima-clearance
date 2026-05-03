using Serilog.Context;

namespace B2.Server.Api.Middleware;

/// <summary>
/// Establishes a per-request correlation id. Honors the W3C Trace-Context
/// <c>traceparent</c> header (RFC 9110, W3C Trace Context Level 2) by extracting
/// the trace-id and reusing it as the correlation id; falls back to the legacy
/// <c>X-Correlation-Id</c> header for older clients; otherwise generates a fresh id.
/// Both the legacy header and a <c>traceparent</c> are emitted on the response so
/// downstream tooling and existing operator clients keep working.
/// T-S1-04 (W3C Trace-Context propagation).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string LegacyHeader = "X-Correlation-Id";
    public const string TraceParentHeader = "traceparent";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;
        string? incomingTraceParent = null;

        if (context.Request.Headers.TryGetValue(TraceParentHeader, out var tpValues) &&
            TryExtractTraceId(tpValues.ToString(), out var traceId))
        {
            correlationId = traceId;
            incomingTraceParent = tpValues.ToString();
        }
        else if (context.Request.Headers.TryGetValue(LegacyHeader, out var legacy) &&
            !string.IsNullOrWhiteSpace(legacy))
        {
            correlationId = legacy.ToString();
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = correlationId;
        context.Response.Headers.TryAdd(LegacyHeader, correlationId);

        if (incomingTraceParent is not null)
        {
            context.Response.Headers.TryAdd(TraceParentHeader, incomingTraceParent);
        }
        else if (TryFormatTraceParent(correlationId, out var newTp))
        {
            context.Response.Headers.TryAdd(TraceParentHeader, newTp);
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Parses a <c>traceparent</c> header value (<c>00-{32 hex trace-id}-{16 hex span-id}-{2 hex flags}</c>)
    /// and returns the 32-character trace-id (in lower hex), rejecting the all-zero invalid id.
    /// </summary>
    internal static bool TryExtractTraceId(string traceParent, out string traceId)
    {
        traceId = string.Empty;
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return false;
        }

        var parts = traceParent.Split('-');
        if (parts.Length < 4)
        {
            return false;
        }

        var candidate = parts[1];
        if (candidate.Length != 32 || !IsLowerHex(candidate) || candidate == new string('0', 32))
        {
            return false;
        }

        traceId = candidate;
        return true;
    }

    private static bool TryFormatTraceParent(string correlationId, out string traceParent)
    {
        traceParent = string.Empty;
        if (correlationId.Length != 32 || !IsLowerHex(correlationId))
        {
            return false;
        }

        // Span-id from a fresh GUID's upper 8 bytes; flags = 00 (not sampled).
        var spanId = Guid.NewGuid().ToString("N").Substring(0, 16);
        traceParent = $"00-{correlationId}-{spanId}-00";
        return true;
    }

    private static bool IsLowerHex(string value)
    {
        foreach (var ch in value)
        {
            if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
