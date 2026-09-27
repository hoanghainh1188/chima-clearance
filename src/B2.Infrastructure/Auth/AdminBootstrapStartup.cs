using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.Auth;
using B2.Domain.Audit;
using B2.Domain.Users;
using B2.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace B2.Infrastructure.Auth;

/// <summary>
/// F-01 — runs <see cref="AdminPasswordBootstrapper"/> once on service start (after
/// migrations) and writes the FR-003 audit row directly, since there is no HTTP request whose
/// audit middleware could persist it. A failure is logged, never fatal: the service must keep
/// serving the exit gate; the operator recovers with <c>B2Clearance.exe reset-initial-admin</c>.
/// </summary>
public static class AdminBootstrapStartup
{
    public static async Task RunAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = services.CreateScope();
            var bootstrapper = scope.ServiceProvider.GetRequiredService<AdminPasswordBootstrapper>();
            var (outcome, adminId) = await bootstrapper.RunAsync(cancellationToken);

            if (outcome == AdminBootstrapOutcome.Applied && adminId is { } id)
            {
                await PersistAuditAsync(scope.ServiceProvider, id, cancellationToken);
                logger.LogInformation("F-01: initial Admin password applied from the installer.");
            }
            else if (outcome == AdminBootstrapOutcome.RejectedWeakPassword)
            {
                logger.LogWarning("F-01: pending initial Admin password failed complexity rules and was discarded.");
            }
            else
            {
                logger.LogInformation("F-01: admin bootstrap outcome {Outcome}.", outcome);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "F-01: applying the initial Admin password failed; the service continues.");
        }
    }

    private static async Task PersistAuditAsync(IServiceProvider services, Guid adminId, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<B2DbContext>();
        var clock = services.GetRequiredService<IClock>();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = nameof(User),
            EntityId = adminId,
            Action = AuditAction.Update,
            ChangesJson = JsonSerializer.Serialize(new { initialPasswordProvisioned = true, mustChangePassword = true }),
            Reason = "F-01 per-install initial password",
            ActorUserId = null,
            At = clock.UtcNow,
            CorrelationId = null,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
