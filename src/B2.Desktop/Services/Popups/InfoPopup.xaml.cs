using CommunityToolkit.Maui.Views;

namespace B2.Desktop.Services.Popups;

/// <summary>
/// Read-only popup with a single Close button per wireframe § 7. Backdrop
/// click and Esc both dismiss; the popup is never destructive.
/// </summary>
public partial class InfoPopup : Popup
{
    public InfoPopup(string title, string message, string close)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        CloseButton.Text = close;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await CloseAsync().ConfigureAwait(true);
    }
}
