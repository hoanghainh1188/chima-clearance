namespace B2.Desktop.Services;

/// <summary>
/// Builds the post-login shell. Identity (role / full name) is read from
/// <see cref="ISessionContext"/> at construction time, not passed in here.
/// </summary>
public interface IMainShellFactory
{
    Page CreateMainShell();

    /// <summary>
    /// Builds the forced password-change page (FR-017) hosted in a
    /// <see cref="NavigationPage"/>, used when login returns
    /// <c>MustChangePassword</c>. The page cannot be cancelled out of.
    /// </summary>
    Page CreateForcedPasswordChange();
}
