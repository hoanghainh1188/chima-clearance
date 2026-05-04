using CommunityToolkit.Maui.Alerts;

namespace B2.Desktop.Services;

public sealed class ToastService : IToastService
{
    public async Task ShowAsync(string message, CancellationToken cancellationToken = default)
    {
        var toast = Toast.Make(message);
        await toast.Show(cancellationToken).ConfigureAwait(true);
    }
}
