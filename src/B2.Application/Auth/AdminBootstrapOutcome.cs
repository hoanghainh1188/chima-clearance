namespace B2.Application.Auth;

/// <summary>F-01 — what <see cref="AdminPasswordBootstrapper"/> did on service start.</summary>
public enum AdminBootstrapOutcome
{
    /// <summary>A pending initial password was applied to the Admin account.</summary>
    Applied = 0,

    /// <summary>No pending password; the Admin still has an initial password.</summary>
    NoPendingPassword = 1,

    /// <summary>The Admin already chose their own password; any pending one was discarded.</summary>
    AdminAlreadyRotated = 2,

    /// <summary>No <c>admin</c> account exists; any pending password was discarded.</summary>
    AdminMissing = 3,

    /// <summary>The pending password failed the complexity rules and was discarded.</summary>
    RejectedWeakPassword = 4,
}
