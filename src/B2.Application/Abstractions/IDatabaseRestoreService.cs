namespace B2.Application.Abstractions;

/// <summary>
/// F-15 restore staging. A restore is applied <b>on next startup</b> (before EF opens the
/// DB), not mid-run — that avoids swapping a SQLite file the app currently holds open.
/// </summary>
public interface IDatabaseRestoreService
{
    /// <summary>Stages a restore: <paramref name="writeDatabase"/> streams the decrypted SQLite DB
    /// into a temp file (the stream is readable and seekable, so the writer can validate what it
    /// wrote); only if it completes is the file promoted to the pending path with its audit
    /// metadata. If it throws, the temp file is deleted and nothing is staged.</summary>
    Task StageAsync(
        Func<Stream, CancellationToken, Task> writeDatabase,
        RestoreStagingMetadata metadata,
        CancellationToken cancellationToken);

    /// <summary>If a staged restore exists, swaps it into the live DB path (clearing pools
    /// and WAL sidecars first). Returns the staged metadata when a swap occurred; otherwise null.
    /// Called once at startup before the DB opens.</summary>
    RestoreStagingMetadata? ApplyPending();

    /// <summary>Cleans up the pre-restore backup after startup migrations succeed.</summary>
    void CommitPending();

    /// <summary>Rolls back to the pre-restore database if startup migrations or checks fail.</summary>
    void RollbackPending();

    /// <summary>True while a restore is staged and awaiting apply (writes must be refused).</summary>
    bool IsRestorePending();
}
