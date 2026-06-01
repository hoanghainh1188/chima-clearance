using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Audit;

namespace B2.Application.Admin.Restore;

/// <summary>
/// F-15 — decrypts an uploaded snapshot with the install password OR recovery code,
/// validates it is a SQLite database, and stages it for apply-on-next-startup. Writes a
/// <see cref="AuditAction.Restore"/> audit row.
/// </summary>
public sealed class RestoreCommandHandler
{
    private static readonly byte[] SqliteMagic = "SQLite format 3\0"u8.ToArray();

    private readonly IBackupSnapshotService _backups;
    private readonly IDatabaseRestoreService _restore;
    private readonly IAuditContext _audit;

    public RestoreCommandHandler(IBackupSnapshotService backups, IDatabaseRestoreService restore, IAuditContext audit)
    {
        _backups = backups;
        _restore = restore;
        _audit = audit;
    }

    public void Handle(byte[] container, string secret, Guid? actorUserId)
    {
        var databaseBytes = _backups.DecryptSnapshot(container, secret);
        if (databaseBytes.Length < SqliteMagic.Length
            || !databaseBytes.AsSpan(0, SqliteMagic.Length).SequenceEqual(SqliteMagic))
        {
            throw new DomainException("BACKUP_NOT_SQLITE", "The decrypted snapshot is not a SQLite database.", 400);
        }

        _restore.Stage(databaseBytes);
        _audit.Set(new AuditEnvelope(
            actorUserId,
            AuditAction.Restore,
            "DatabaseRestore",
            Guid.NewGuid(),
            null,
            "{\"staged\":true}",
            null));
    }
}
