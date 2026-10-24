using System.Globalization;
using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.Admin.Audit;
using B2.Application.Admin.Health;
using B2.Application.Admin.LogViewer;
using B2.Application.Admin.Restore;
using B2.Domain.Audit;
using B2.Server.Api.Auth;
using B2.Server.Api.Hosting;
using Microsoft.AspNetCore.Http.Features;

namespace B2.Server.Api.Endpoints;

public static class AdminEndpoints
{
    private const int MaxPageSize = 200;

    // ADR-0011 — a restore upload may be a few hundred MB; Kestrel's 30 MB body default and the
    // 128 MB multipart default would reject it. Raised for /restore only.
    private const long MaxSnapshotUploadBytes = 2L * 1024 * 1024 * 1024;
    private const int FileBufferBytes = 81_920;

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

        // F-18 — recent log lines, newest-first, optionally filtered by level.
        g.MapGet(
            "/logs",
            (HttpRequest request, LogsQueryHandler handler) =>
            {
                var query = request.Query;
                var level = NullIfWhitespace(query["level"].ToString());
                var page = ReadPositiveInt(query, "page", 1, int.MaxValue);
                var pageSize = ReadPositiveInt(query, "pageSize", 100, MaxPageSize);
                return Results.Ok(handler.Handle(level, page, pageSize));
            });

        // F-18 — health dashboard detail (uptime, memory, DB size, free disk, open > 24 h).
        g.MapGet(
            "/health/detail",
            async (HealthQueryHandler handler, CancellationToken ct) => Results.Ok(await handler.HandleAsync(ct)));

        // F-15 / FR-026 — on-demand encrypted (dual-wrapped) snapshot download.
        g.MapPost(
            "/backup-now",
            async (
                HttpContext http,
                IBackupSnapshotService backups,
                IBackupSecretsStore secrets,
                IClock clock,
                IAuditContext audit,
                CancellationToken ct) =>
            {
                var keys = secrets.GetOrCreate();

                // ADR-0011: stream to a self-deleting temp file, not RAM, and only answer 200
                // once the whole container is written (a mid-stream failure must not reach the
                // client as a truncated 200 body).
                var output = CreateTempFile("b2-backup");
                try
                {
                    await backups.CreateSnapshotAsync(keys.InstallPassword, keys.RecoveryCode, output, ct);
                    output.Position = 0;
                }
                catch
                {
                    await output.DisposeAsync();
                    throw;
                }

                var fileName = string.Create(CultureInfo.InvariantCulture, $"b2-{clock.UtcNow:yyyyMMdd-HHmm}.bak");
                audit.Set(new AuditEnvelope(
                    http.User.GetB2UserId(),
                    AuditAction.Create,
                    "BackupSnapshot",
                    Guid.NewGuid(),
                    null,
                    JsonSerializer.Serialize(new { fileName, output.Length }),
                    null));
                return Results.File(output, "application/octet-stream", fileName);
            });

        // F-15 — restore from an uploaded snapshot (password OR recovery code). With neither,
        // use this PC's own sealed install password: restoring on the entry-gate PC itself
        // must not depend on the recovery-code envelope (that is for a replaced PC). The
        // staged DB is applied on the next startup; outside tests we trigger that restart now.
        g.MapPost(
            "/restore",
            async (
                HttpContext http,
                RestoreCommandHandler handler,
                IBackupSecretsStore secrets,
                IServiceRestarter restarter,
                CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType)
                {
                    return Results.BadRequest(new { code = "INVALID_REQUEST" });
                }

                AllowLargeSnapshotUpload(http);
                var form = await http.Request.ReadFormAsync(ct);
                var file = form.Files["file"];
                if (file is null || file.Length == 0)
                {
                    return Results.BadRequest(new { code = "FILE_REQUIRED" });
                }

                var secret = form["password"].ToString();
                if (string.IsNullOrEmpty(secret))
                {
                    secret = form["recoveryCode"].ToString();
                }

                if (string.IsNullOrEmpty(secret))
                {
                    secret = secrets.GetOrCreate().InstallPassword;
                }

                // The form reader spools the upload to disk above 64 KiB; decrypt streams from it.
                await using (var container = file.OpenReadStream())
                {
                    await handler.HandleAsync(container, secret, http.User.GetB2UserId(), file.FileName, ct);
                }

                // Bug #173: the restarter flushes this 200 first, then restarts the host.
                restarter.RequestRestart();

                return Results.Ok(new { restartRequired = true });
            });
    }

    private static FileStream CreateTempFile(string prefix) =>
        new(
            Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            FileBufferBytes,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

    private static void AllowLargeSnapshotUpload(HttpContext http)
    {
        var bodySize = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (bodySize is { IsReadOnly: false })
        {
            bodySize.MaxRequestBodySize = MaxSnapshotUploadBytes;
        }

        if (http.Features.Get<IFormFeature>()?.Form is null)
        {
            http.Features.Set<IFormFeature>(new FormFeature(
                http.Request,
                new FormOptions { MultipartBodyLengthLimit = MaxSnapshotUploadBytes }));
        }
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
