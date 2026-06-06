namespace B2.Application.Abstractions;

/// <summary>
/// F-15 restore staging. A restore is applied <b>on next startup</b> (before EF opens the
/// DB), not mid-run — that avoids swapping a SQLite file the app currently holds open.
/// </summary>
public interface IDatabaseRestoreService
{
    /// <summary>Writes the decrypted SQLite bytes and restore audit metadata to staging files.</summary>
    void Stage(byte[] databaseBytes, RestoreStagingMetadata metadata);

    /// <summary>If a staged restore exists, swaps it into the live DB path (clearing pools
    /// and WAL sidecars first). Returns the staged metadata when a swap occurred; otherwise null.
    /// Called once at startup before the DB opens.</summary>
    RestoreStagingMetadata? ApplyPending();

    /// <summary>True while a restore is staged and awaiting apply (writes must be refused).</summary>
    bool IsRestorePending();
}
