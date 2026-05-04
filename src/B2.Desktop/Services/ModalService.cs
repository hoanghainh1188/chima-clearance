namespace B2.Desktop.Services;

public sealed class ModalService : IModalService
{
    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel,
        CancellationToken cancellationToken = default)
    {
        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return Task.FromResult(false);
        }

        return page.DisplayAlert(title, message, accept, cancel);
    }

    public Task ShowAsync(
        string title,
        string message,
        string close,
        CancellationToken cancellationToken = default)
    {
        var page = Application.Current?.MainPage;
        if (page is null)
        {
            return Task.CompletedTask;
        }

        return page.DisplayAlert(title, message, close);
    }
}
