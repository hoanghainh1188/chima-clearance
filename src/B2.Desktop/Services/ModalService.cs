using B2.Desktop.Services.Popups;
using CommunityToolkit.Maui.Views;

namespace B2.Desktop.Services;

/// <summary>
/// Modal service using <see cref="CommunityToolkit.Maui.Views.Popup"/>.
/// Replaces the previous <c>DisplayAlert</c> wrappers so wireframe § 7
/// requirements (focus trap, Esc / backdrop dismissal, return-focus to
/// trigger) hold. CommunityToolkit.Maui's Popup enforces focus trap on
/// Windows out of the box; backdrop dismissal is gated by the
/// <c>destructive</c> flag passed in.
/// </summary>
public sealed class ModalService : IModalService, IDisposable
{
    // #188: CommunityToolkit allows one popup at a time ("Only one popup can be displayed at a
    // time" otherwise). Queue concurrent requests instead of crashing the caller.
    private readonly SemaphoreSlim _onePopup = new(1, 1);

    // ShowPopupAsync MUST run on the UI thread. ViewModel commands frequently
    // reach these methods from a thread-pool continuation (await ...
    // ConfigureAwait(false)); showing a popup off the UI thread throws and, in
    // an async-void command, crashes the app. Marshal the whole body so every
    // caller is safe regardless of its current synchronization context.
    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel,
        bool destructive = false,
        CancellationToken cancellationToken = default) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.MainPage;
            if (page is null)
            {
                return false;
            }

            await _onePopup.WaitAsync(cancellationToken).ConfigureAwait(true);
            try
            {
                var popup = new ConfirmPopup(title, message, accept, cancel, destructive);
                var result = await page.ShowPopupAsync(popup, token: cancellationToken).ConfigureAwait(true);
                return result is true;
            }
            finally
            {
                _onePopup.Release();
            }
        });

    public Task ShowAsync(
        string title,
        string message,
        string close,
        CancellationToken cancellationToken = default) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.MainPage;
            if (page is null)
            {
                return;
            }

            await _onePopup.WaitAsync(cancellationToken).ConfigureAwait(true);
            try
            {
                var popup = new InfoPopup(title, message, close);
                await page.ShowPopupAsync(popup, token: cancellationToken).ConfigureAwait(true);
            }
            finally
            {
                _onePopup.Release();
            }
        });

    public void Dispose() => _onePopup.Dispose();
}
