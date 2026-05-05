using B2.Application.Abstractions;

namespace B2.Server.Api.Audit;

public sealed class HttpAuditContext : IAuditContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public const string ItemKey = "B2.AuditEnvelope";

    public HttpAuditContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Set(AuditEnvelope envelope)
    {
        // Quartz jobs (CreateDailyHeader, GenerateDailySummary) and the startup
        // catch-up host invoke handlers outside the HTTP pipeline. FR-003 binds
        // the AuditLog to "successful mutating requests"; cron-driven runs leave
        // their trail in the data itself (the header row appears at midnight).
        // No-op without HttpContext rather than crashing the host.
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            return;
        }

        ctx.Items[ItemKey] = envelope;
    }

    public static AuditEnvelope? TryGet(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ItemKey, out var v) && v is AuditEnvelope e ? e : null;
}
