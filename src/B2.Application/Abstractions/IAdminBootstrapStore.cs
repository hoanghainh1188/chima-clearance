namespace B2.Application.Abstractions;

/// <summary>
/// F-01 — the hand-off between the installer and the service for the per-install initial
/// Admin password. The installer seals a pending password; the service applies it on start
/// and records whether the Admin still uses an initial password, which the installer reads
/// to decide whether an upgrade must issue a new one.
/// </summary>
public interface IAdminBootstrapStore
{
    /// <summary>Returns the pending initial password written by the installer, if any.</summary>
    string? TryReadPendingPassword();

    /// <summary>Deletes the pending password (after it was applied or deliberately ignored).</summary>
    void DeletePending();

    /// <summary>The last recorded state, or <c>null</c> before the first F-01 start.</summary>
    AdminPasswordState? ReadState();

    /// <summary>
    /// Records the Admin password state for the installer to read. Best effort: a write
    /// failure is logged by the implementation, never thrown to the caller.
    /// </summary>
    void WriteState(AdminPasswordState state);
}
