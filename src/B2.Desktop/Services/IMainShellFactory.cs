namespace B2.Desktop.Services;

/// <summary>
/// Builds the post-login shell. Identity (role / full name) is read from
/// <see cref="ISessionContext"/> at construction time, not passed in here.
/// </summary>
public interface IMainShellFactory
{
    Page CreateMainShell();
}
