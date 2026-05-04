namespace B2.Desktop.Services;

/// <summary>
/// Encapsulates the "send the user back to login" navigation. The pre-login
/// path uses <c>Shell.Current.GoToAsync("//login")</c> within
/// <see cref="AppShell"/>; the post-login path replaces
/// <c>Application.Current.MainPage</c> with a fresh <see cref="AppShell"/>.
/// Callers (logout, force-rotate, voluntary password change) should not have
/// to know which they are in.
/// </summary>
public interface IAppNavigator
{
    Task NavigateToLoginAsync(CancellationToken cancellationToken = default);
}
