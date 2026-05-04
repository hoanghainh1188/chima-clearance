namespace B2.Desktop.Services;

public sealed class AppNavigator : IAppNavigator
{
    private readonly IServiceProvider _services;

    public AppNavigator(IServiceProvider services) => _services = services;

    public async Task NavigateToLoginAsync(CancellationToken cancellationToken = default)
    {
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync("//login").ConfigureAwait(false);
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Application.Current!.MainPage = _services.GetRequiredService<AppShell>();
        }).ConfigureAwait(false);
    }
}
