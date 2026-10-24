using CommunityToolkit.Maui.Views;

namespace B2.Desktop.Services.Popups;

/// <summary>
/// Yes/No confirmation popup per wireframe § 7. Backdrop click and Esc
/// dismiss the popup unless <see cref="DestructiveDefaultsToCancel"/> is
/// true (set via the constructor's <c>destructive</c> flag) — in which case
/// the popup auto-resolves to <c>false</c> on dismissal but the user can
/// still close via the Cancel button. CommunityToolkit.Maui's Popup
/// already enforces focus trap on Windows.
/// </summary>
public partial class ConfirmPopup : Popup
{
    // #188: a double-click fires the handler twice; the second CloseAsync on a closing popup
    // throws InvalidOperationException inside async void and crashes the app.
    private bool _closing;

    public ConfirmPopup(string title, string message, string accept, string cancel, bool destructive)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        AcceptButton.Text = accept;
        CancelButton.Text = cancel;
        CanBeDismissedByTappingOutsideOfPopup = !destructive;
    }

    private async void OnAcceptClicked(object? sender, EventArgs e) => await CloseOnceAsync(true).ConfigureAwait(true);

    private async void OnCancelClicked(object? sender, EventArgs e) => await CloseOnceAsync(false).ConfigureAwait(true);

    private async Task CloseOnceAsync(bool result)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        AcceptButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        await CloseAsync(result).ConfigureAwait(true);
    }
}
