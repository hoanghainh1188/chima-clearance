using B2.Desktop.ViewModels;
using B2.Desktop.Views;

namespace B2.Desktop.Services;

public sealed class MainShellFactory : IMainShellFactory
{
    private readonly IServiceProvider _services;

    public MainShellFactory(IServiceProvider services) => _services = services;

    public Page CreateMainShell() => new MainShellPage(_services);

    public Page CreateForcedPasswordChange()
    {
        var page = _services.GetRequiredService<ChangePasswordPage>();
        if (page.BindingContext is ChangePasswordViewModel vm)
        {
            vm.CanCancel = false;
        }

        return new NavigationPage(page);
    }
}
