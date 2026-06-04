using B2.Application.Abstractions;
using B2.Domain;

namespace B2.Application.Admin.Restore;

/// <summary>
/// F-15 — decrypts an uploaded snapshot with the install password OR recovery code,
/// validates it is a SQLite database, and stages it (with audit metadata) for apply-on-next-startup.
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

    public void Handle(byte[] container, string secret, Guid? actorUserId, string? snapshotFileName)
    {
        var databaseBytes = _backups.DecryptSnapshot(container, secret);
        if (databaseBytes.Length < SqliteMagic.Length
            || !databaseBytes.AsSpan(0, SqliteMagic.Length).SequenceEqual(SqliteMagic))
        {
            throw new DomainException("BACKUP_NOT_SQLITE", "The decrypted snapshot is not a SQLite database.", 400);
        }

        _restore.Stage(
            databaseBytes,
            new RestoreStagingMetadata(actorUserId, _clock.UtcNow, snapshotFileName));
    }
}
