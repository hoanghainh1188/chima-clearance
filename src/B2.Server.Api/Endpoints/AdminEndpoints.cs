using System.Globalization;
using B2.Application.Admin.Audit;
using B2.Domain.Audit;
using B2.Server.Api.Auth;

namespace B2.Server.Api.Endpoints;

public static class AdminEndpoints
{
    private const int MaxPageSize = 200;

    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization(B2Policies.AdminOnly);

        // F-16 — read-only audit-log viewer (append-only; no update/delete surface, FR-003).
        g.MapGet(
            "/audit",
            async (HttpRequest request, ListAuditQueryHandler handler, CancellationToken ct) =>
            {
                var query = request.Query;
                var filter = new AuditLogFilter(
                    NullIfWhitespace(query["entityType"].ToString()),
                    TryGuid(query["entityId"].ToString()),
                    TryGuid(query["actorUserId"].ToString()),
                    TryAction(query["action"].ToString()),
                    TryTimestamp(query["from"].ToString()),
                    TryTimestamp(query["to"].ToString()));
                var page = ReadPositiveInt(query, "page", 1, int.MaxValue);
                var pageSize = ReadPositiveInt(query, "pageSize", 50, MaxPageSize);
                return Results.Ok(await handler.HandleAsync(filter, page, pageSize, ct));
            });
    }

    private static string? NullIfWhitespace(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static Guid? TryGuid(string? s) => Guid.TryParse(s, out var g) ? g : null;

    private static AuditAction? TryAction(string? s) =>
        Enum.TryParse<AuditAction>(s, ignoreCase: true, out var a) ? a : null;

    private static DateTimeOffset? TryTimestamp(string? s) =>
        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;

    private static int ReadPositiveInt(IQueryCollection query, string key, int fallback, int max)
    {
        if (!int.TryParse(query[key].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) || v <= 0)
        {
            return fallback;
        }

        return Math.Min(v, max);
    }
}
