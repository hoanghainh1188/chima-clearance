using B2.Desktop.Resources.Strings;
using B2.Desktop.Views;

namespace B2.Desktop;

public partial class AppShell : Shell
{
    public AppShell(LoginPage loginPage)
    {
        InitializeComponent();
        Title = AppResources.Shell_AppTitle;
        Items.Add(
            new ShellContent
            {
                Title = AppResources.Shell_LoginTab,
                Content = loginPage,
                Route = "login",
            });
        Routing.RegisterRoute("auth/change-password", typeof(ChangePasswordPage));
    }
}
