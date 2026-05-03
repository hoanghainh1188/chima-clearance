using B2.Desktop.Resources.Strings;
using B2.Desktop.Views;
using B2.Desktop.Views.Admin;

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
        Routing.RegisterRoute("admin/users", typeof(UsersPage));
        Routing.RegisterRoute("auth/change-password", typeof(ChangePasswordPage));
    }
}
