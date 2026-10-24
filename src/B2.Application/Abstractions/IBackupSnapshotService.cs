namespace B2.Application.Abstractions;

/// <summary>
/// F-15 / FR-026 — on-demand encrypted database snapshots. The DB is AES-256-GCM
/// encrypted under a single random data key, which is itself wrapped twice (with the
/// install password and with the printable recovery code) so either secret can restore.
/// Both directions stream (ADR-0011): memory use does not grow with the database size.
/// </summary>
public interface IBackupSnapshotService
{
    /// <summary>Snapshots the live DB and writes the encrypted, dual-wrapped v2 container to
    /// <paramref name="destination"/>.</summary>
    Task CreateSnapshotAsync(string installPassword, string recoveryCode, Stream destination, CancellationToken cancellationToken);

    /// <summary>Decrypts a v2 (or legacy v1) container with either the install password OR the
    /// recovery code, writing the raw SQLite database to <paramref name="destination"/>. Throws
    /// if neither secret unwraps the key or the container is truncated/tampered; on failure the
    /// destination may hold partial plaintext and MUST be discarded.</summary>
    Task DecryptSnapshotAsync(Stream container, string secret, Stream destination, CancellationToken cancellationToken);
}
