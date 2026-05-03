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
        var ctx = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No HttpContext.");
        ctx.Items[ItemKey] = envelope;
    }

    public static AuditEnvelope? TryGet(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ItemKey, out var v) && v is AuditEnvelope e ? e : null;
}
