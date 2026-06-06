namespace B2.Desktop.Services;

/// <summary>
/// <see cref="IToastService"/> rendered as an in-app MAUI overlay by the current
/// <see cref="IToastPresenter"/> (the post-login shell). The CommunityToolkit
/// Snackbar was dropped: it needs a Package.appxmanifest / package identity that
/// this unpackaged (WindowsPackageType=None) build does not have, so it threw on
/// every call. Durations follow wireframe § 7 (4 s success/info, 8 s warning,
/// longer for errors which are also tap-to-dismiss).
/// </summary>
public sealed class ToastService : IToastService
{
    private static readonly TimeSpan SuccessDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan WarningDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(12);

    public Task ShowAsync(string message, CancellationToken cancellationToken = default) =>
        ShowAsync(message, ToastSeverity.Info, cancellationToken);

    public Task ShowAsync(
        string message,
        ToastSeverity severity,
        CancellationToken cancellationToken = default)
    {
        var duration = severity switch
        {
            ToastSeverity.Error => ErrorDuration,
            ToastSeverity.Warning => WarningDuration,
            _ => SuccessDuration,
        };

        // Marshal to the UI thread (callers often await with ConfigureAwait(false))
        // and render on the current presenter. If no shell is mounted (e.g. the
        // login / change-password screens, which use inline error labels), the
        // toast is simply skipped — never an exception.
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (Application.Current?.MainPage is IToastPresenter presenter)
            {
                presenter.ShowToast(message, severity, duration);
            }
        });
    }
}
