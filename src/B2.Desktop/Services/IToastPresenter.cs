namespace B2.Desktop.Services;

/// <summary>
/// Implemented by the post-login shell so <see cref="ToastService"/> can render
/// an in-app toast overlay. A pure-MAUI overlay (not the CommunityToolkit
/// Snackbar) is required because Snackbar relies on Windows notifications that
/// need package identity — unavailable in this unpackaged (WindowsPackageType=None)
/// build.
/// </summary>
public interface IToastPresenter
{
    void ShowToast(string message, ToastSeverity severity, TimeSpan duration);
}
