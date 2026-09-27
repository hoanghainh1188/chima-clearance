namespace B2.Application.Abstractions;

/// <summary>F-01 — whether the seeded Admin still has an initial (never self-chosen) password.</summary>
public enum AdminPasswordState
{
    /// <summary><c>MustChangePassword</c> is still true: the installer may issue a new initial password.</summary>
    Initial = 0,

    /// <summary>The Admin chose their own password: the installer must never replace it.</summary>
    Rotated = 1,
}
