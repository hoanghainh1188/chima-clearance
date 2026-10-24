using System.Text.Json;
using B2.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace B2.Infrastructure.Backup;

/// <summary>
/// F-15 restore staging + apply-on-startup. <see cref="Stage"/> drops the decrypted DB at
/// <c>{db}.restore-pending</c> plus a JSON sidecar for post-restore audit; <see cref="ApplyPending"/>
/// (run before EF opens the DB) atomically promotes the pending file via <see cref="File.Replace"/>
/// when a live DB already exists. Doing the swap before any connection opens avoids file-lock conflicts.
/// </summary>
public sealed class DatabaseRestoreService : IDatabaseRestoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _dbPath;
    private readonly string _pendingPath;
    private readonly string _metadataPath;

    public DatabaseRestoreService(string dbPath)
    {
        _dbPath = dbPath;
        _pendingPath = dbPath + ".restore-pending";
        _metadataPath = _pendingPath + ".meta.json";
    }

    public void Stage(byte[] databaseBytes, RestoreStagingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(databaseBytes);
        ArgumentNullException.ThrowIfNull(metadata);

        var directory = Path.GetDirectoryName(_pendingPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = _pendingPath + ".tmp";
        File.WriteAllBytes(temp, databaseBytes);
        File.Move(temp, _pendingPath, overwrite: true);
        File.WriteAllText(_metadataPath, JsonSerializer.Serialize(metadata, JsonOptions));
    }

    public bool IsRestorePending() => File.Exists(_pendingPath);

    public RestoreStagingMetadata? ApplyPending()
    {
        if (!File.Exists(_pendingPath))
        {
            return null;
        }

        var metadata = ReadMetadataOrDefault();
        SqliteConnection.ClearAllPools();

        foreach (var sidecar in new[] { _dbPath + "-wal", _dbPath + "-shm" })
        {
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }

        var preRestoreBackup = _dbPath + ".pre-restore";
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Replace(_pendingPath, _dbPath, preRestoreBackup, ignoreMetadataErrors: true);
                // Bug #138 fix: Keep preRestoreBackup until EnsureDatabaseAsync succeeds.
            }
            else
            {
                var directory = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(_pendingPath, _dbPath);
            }

            DeleteMetadataFile();
            return metadata;
        }
        catch
        {
            RollbackPending();
            throw;
        }
    }

    public void CommitPending()
    {
        var preRestoreBackup = _dbPath + ".pre-restore";
        if (File.Exists(preRestoreBackup))
        {
            try
            {
                File.Delete(preRestoreBackup);
            }
            catch
            {
                // Non-fatal if OS holds a temporary lock
            }
        }
    }

    public void RollbackPending()
    {
        var preRestoreBackup = _dbPath + ".pre-restore";
        if (File.Exists(preRestoreBackup))
        {
            SqliteConnection.ClearAllPools();
            foreach (var sidecar in new[] { _dbPath + "-wal", _dbPath + "-shm" })
            {
                if (File.Exists(sidecar))
                {
                    File.Delete(sidecar);
                }
            }

            File.Move(preRestoreBackup, _dbPath, overwrite: true);
        }
    }

    private RestoreStagingMetadata ReadMetadataOrDefault()
    {
        if (!File.Exists(_metadataPath))
        {
            return new RestoreStagingMetadata(null, DateTimeOffset.UtcNow, null);
        }

        try
        {
            return JsonSerializer.Deserialize<RestoreStagingMetadata>(File.ReadAllText(_metadataPath), JsonOptions)
                ?? new RestoreStagingMetadata(null, DateTimeOffset.UtcNow, null);
        }
        catch (JsonException)
        {
            return new RestoreStagingMetadata(null, DateTimeOffset.UtcNow, null);
        }
    }

    private void DeleteMetadataFile()
    {
        if (File.Exists(_metadataPath))
        {
            File.Delete(_metadataPath);
        }
    }
}
