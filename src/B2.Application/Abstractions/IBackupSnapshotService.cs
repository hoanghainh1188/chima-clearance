namespace B2.Application.Abstractions;

/// <summary>
/// F-15 / FR-026 — on-demand encrypted database snapshots. The DB is AES-256-GCM
/// encrypted under a single random data key, which is itself wrapped twice (with the
/// install password and with the printable recovery code) so either secret can restore.
/// </summary>
public interface IBackupSnapshotService
{
    /// <summary>Snapshots the live DB and returns the encrypted, dual-wrapped container bytes.</summary>
    Task<byte[]> CreateSnapshotAsync(string installPassword, string recoveryCode, CancellationToken cancellationToken);

    /// <summary>Decrypts a container with either the install password OR the recovery code,
    /// returning the raw SQLite database bytes. Throws if neither secret unwraps the key.</summary>
    byte[] DecryptSnapshot(byte[] container, string secret);
}
