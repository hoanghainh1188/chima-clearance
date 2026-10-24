using B2.Application.Abstractions;
using B2.Domain;
using Microsoft.Data.Sqlite;

namespace B2.Infrastructure.Backup;

/// <summary>
/// F-15 / FR-026 encrypted snapshot codec. A random 32-byte data key (DK) encrypts the
/// SQLite bytes with AES-256-GCM; DK is wrapped twice (PBKDF2-SHA256 → AES-256-GCM) under
/// the install password and the recovery code, so either secret decrypts the snapshot.
/// New snapshots use the chunked v2 container (<see cref="SnapshotContainerV2"/>, ADR-0011);
/// restore also accepts the legacy v1 JSON container, detected by its leading <c>{</c>.
/// </summary>
public sealed class BackupSnapshotService : IBackupSnapshotService
{
    private const int CopyBufferBytes = 81_920;

    private readonly string _dbPath;

    public BackupSnapshotService(string dbPath) => _dbPath = dbPath;

    public async Task CreateSnapshotAsync(string installPassword, string recoveryCode, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (string.IsNullOrEmpty(installPassword) || string.IsNullOrEmpty(recoveryCode))
        {
            throw new DomainException("BACKUP_SECRET_MISSING", "Install password and recovery code are both required.", 400);
        }

        var tempPath = await SnapshotDatabaseAsync(cancellationToken);
        try
        {
            await using var source = new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await SnapshotContainerV2.WriteAsync(source, destination, installPassword, recoveryCode, cancellationToken);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    public async Task DecryptSnapshotAsync(Stream container, string secret, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(destination);

        var magic = new byte[SnapshotContainerV2.MagicBytes];
        var read = await container.ReadAtLeastAsync(magic, magic.Length, throwOnEndOfStream: false, cancellationToken);
        if (read == magic.Length && magic.AsSpan().SequenceEqual(SnapshotContainerV2.Magic))
        {
            await SnapshotContainerV2.DecryptAsync(container, secret, destination, cancellationToken);
        }
        else if (read > 0 && magic[0] == (byte)'{')
        {
            await SnapshotContainerV1.DecryptAsync(magic.AsMemory(0, read), container, secret, destination, cancellationToken);
        }
        else
        {
            throw SnapshotErrors.Corrupt("The file is not a B2 snapshot.");
        }
    }

    /// <summary>Copies the live DB with the SQLite online-backup API to a temp file; returns its path.</summary>
    private async Task<string> SnapshotDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_dbPath))
        {
            throw new DomainException("BACKUP_DB_MISSING", "The database file was not found.", 409);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"b2-snapshot-{Guid.NewGuid():N}.db");
        try
        {
            // Pooling=False on both ends so the connections fully close on dispose and
            // release the temp-file handle before we read it (a pooled connection keeps the
            // OS file lock, which would fail the read below on Windows).
            await using var source = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
            await source.OpenAsync(cancellationToken);
            await using var destination = new SqliteConnection($"Data Source={tempPath};Pooling=False");
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
            return tempPath;
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }
}
