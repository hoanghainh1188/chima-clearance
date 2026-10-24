using B2.Application.Abstractions;
using B2.Domain;

namespace B2.Application.Admin.Restore;

/// <summary>
/// F-15 — decrypts an uploaded snapshot with the install password OR recovery code,
/// streams it into staging, validates it is a SQLite database, and only then stages it (with audit metadata) for apply-on-next-startup.
/// The <see cref="AuditAction.Restore"/> row is written after the DB swap on startup.
/// </summary>
public sealed class RestoreCommandHandler
{
    private static readonly byte[] SqliteMagic = "SQLite format 3\0"u8.ToArray();

    private readonly IBackupSnapshotService _backups;
    private readonly IDatabaseRestoreService _restore;
    private readonly IClock _clock;

    public RestoreCommandHandler(IBackupSnapshotService backups, IDatabaseRestoreService restore, IClock clock)
    {
        _backups = backups;
        _restore = restore;
        _clock = clock;
    }

    public async Task HandleAsync(
        Stream container,
        string secret,
        Guid? actorUserId,
        string? snapshotFileName,
        CancellationToken cancellationToken)
    {
        await _restore.StageAsync(
            async (staging, ct) =>
            {
                await _backups.DecryptSnapshotAsync(container, secret, staging, ct);
                await EnsureSqliteAsync(staging, ct);
            },
            new RestoreStagingMetadata(actorUserId, _clock.UtcNow, snapshotFileName),
            cancellationToken);
    }

    private static async Task EnsureSqliteAsync(Stream staging, CancellationToken cancellationToken)
    {
        var header = new byte[SqliteMagic.Length];
        staging.Position = 0;
        var read = await staging.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (read < header.Length || !header.AsSpan().SequenceEqual(SqliteMagic))
        {
            throw new DomainException("BACKUP_NOT_SQLITE", "The decrypted snapshot is not a SQLite database.", 400);
        }
    }
}
