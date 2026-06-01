using B2.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace B2.Infrastructure.Backup;

/// <summary>
/// F-15 restore staging + apply-on-startup. <see cref="Stage"/> drops the decrypted DB at
/// <c>{db}.restore-pending</c>; <see cref="ApplyPending"/> (run before EF opens the DB)
/// clears the SQLite pool, removes the live DB + WAL/SHM sidecars, and promotes the pending
/// file. Doing the swap before any connection opens avoids file-lock conflicts.
/// </summary>
public sealed class DatabaseRestoreService : IDatabaseRestoreService
{
    private readonly string _dbPath;
    private readonly string _pendingPath;

    public DatabaseRestoreService(string dbPath)
    {
        _dbPath = dbPath;
        _pendingPath = dbPath + ".restore-pending";
    }

    public void Stage(byte[] databaseBytes)
    {
        ArgumentNullException.ThrowIfNull(databaseBytes);
        var directory = Path.GetDirectoryName(_pendingPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _pendingPath + ".tmp";
        File.WriteAllBytes(temp, databaseBytes);
        File.Move(temp, _pendingPath, overwrite: true);
    }

    public bool ApplyPending()
    {
        if (!File.Exists(_pendingPath))
        {
            return false;
        }

        SqliteConnection.ClearAllPools();

        foreach (var sidecar in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
        {
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }

        File.Move(_pendingPath, _dbPath);
        return true;
    }
}
