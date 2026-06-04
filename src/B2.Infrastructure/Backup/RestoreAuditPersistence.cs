using System.Text.Json;
using B2.Application.Abstractions;
using B2.Domain.Audit;
using B2.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Infrastructure.Backup;

/// <summary>
/// Writes the FR-003 / F-15 restore audit row into the database after a staged snapshot
/// has been applied on startup (the HTTP-time audit would be lost when the DB file is replaced).
/// </summary>
public static class RestoreAuditPersistence
{
    public static async Task PersistAfterApplyAsync(IServiceProvider services, RestoreStagingMetadata metadata, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var changesJson = JsonSerializer.Serialize(new
        {
            restored = true,
            snapshotFileName = metadata.SnapshotFileName,
            stagedAtUtc = metadata.StagedAtUtc,
        });

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityType = "DatabaseRestore",
            EntityId = Guid.NewGuid(),
            Action = AuditAction.Restore,
            ChangesJson = changesJson,
            Reason = null,
            ActorUserId = metadata.ActorUserId,
            At = clock.UtcNow,
            CorrelationId = null,
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
