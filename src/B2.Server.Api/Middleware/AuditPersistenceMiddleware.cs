using B2.Application.Abstractions;
using B2.Domain.Audit;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Audit;
using B2.Server.Api.Auth;
using Microsoft.EntityFrameworkCore;

namespace B2.Server.Api.Middleware;

/// <summary>
/// Writes one <see cref="AuditLog"/> row per successful mutating request when an envelope was set.
/// Best-effort: a failed audit save MUST NOT turn a successful 2xx into a 5xx, since the
/// domain write has already committed. Failures are logged with the correlationId and
/// must be reconciled out-of-band (FR-003).
/// </summary>
public sealed class AuditPersistenceMiddleware(RequestDelegate next, ILogger<AuditPersistenceMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory, IClock clock)
    {
        await next(context);

        if (context.Response.StatusCode < 200 || context.Response.StatusCode >= 300)
        {
            return;
        }

        var method = context.Request.Method;
        if (method != HttpMethods.Post && method != HttpMethods.Put && method != HttpMethods.Delete &&
            method != HttpMethods.Patch)
        {
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/admin/health"))
        {
            return;
        }

        var envelope = HttpAuditContext.TryGet(context);
        if (envelope is null)
        {
            return;
        }

        var actor = envelope.Value.ActorUserId
            ?? context.User.GetB2UserId();
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();

            var changesJson = $"{{\"before\":{envelope.Value.BeforeJson ?? "null"},\"after\":{envelope.Value.AfterJson ?? "null"}}}";
            var log = new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = envelope.Value.EntityType,
                EntityId = envelope.Value.EntityId,
                Action = envelope.Value.Action,
                ChangesJson = changesJson,
                Reason = envelope.Value.Reason,
                ActorUserId = actor,
                At = clock.UtcNow,
                CorrelationId = correlationId,
            };

            db.AuditLogs.Add(log);
            await db.SaveChangesAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected after the domain write committed — nothing actionable.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Audit persistence failed after successful write. EntityType={EntityType} EntityId={EntityId} Action={Action} Actor={Actor} CorrelationId={CorrelationId}",
                envelope.Value.EntityType,
                envelope.Value.EntityId,
                envelope.Value.Action,
                actor,
                correlationId);
        }
    }
}
