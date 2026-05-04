using B2.Desktop.Resources.Strings;
using CommunityToolkit.Maui.Alerts;

namespace B2.Desktop.Services;

/// <summary>
/// <see cref="IToastService"/> backed by CommunityToolkit.Maui Snackbar so
/// duration is configurable per severity (wireframe § 7). Position is the
/// MAUI default (bottom-center) — top-right placement is a documented gap
/// that requires a custom overlay manager and is deferred.
/// </summary>
public sealed class ToastService : IToastService
{
    // Per wireframe § 7: 4 s success, 8 s warning, manual dismiss for error.
    private static readonly TimeSpan SuccessDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan WarningDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromHours(1);

    public Task ShowAsync(string message, CancellationToken cancellationToken = default) =>
        ShowAsync(message, ToastSeverity.Info, cancellationToken);

    public async Task ShowAsync(
        string message,
        ToastSeverity severity,
        CancellationToken cancellationToken = default)
    {
        var (duration, withClose) = severity switch
        {
            ToastSeverity.Error => (ErrorDuration, true),
            ToastSeverity.Warning => (WarningDuration, false),
            _ => (SuccessDuration, false),
        };

        var snackbar = withClose
            ? Snackbar.Make(
                message,
                action: () => { /* dismiss handled by Snackbar */ },
                actionButtonText: AppResources.Common_Close,
                duration: duration)
            : Snackbar.Make(
                message,
                action: null,
                actionButtonText: string.Empty,
                duration: duration);

        await snackbar.Show(cancellationToken).ConfigureAwait(true);
    }
}
