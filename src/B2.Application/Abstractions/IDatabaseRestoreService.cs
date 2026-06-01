namespace B2.Application.Abstractions;

/// <summary>
/// F-15 restore staging. A restore is applied <b>on next startup</b> (before EF opens the
/// DB), not mid-run — that avoids swapping a SQLite file the app currently holds open.
/// </summary>
public interface IDatabaseRestoreService
{
    /// <summary>Writes the decrypted SQLite bytes to the pending-restore staging file.</summary>
    void Stage(byte[] databaseBytes);

    /// <summary>If a staged restore exists, swaps it into the live DB path (clearing pools
    /// and WAL sidecars first) and returns true. Called once at startup before the DB opens.</summary>
    bool ApplyPending();
}
