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

        // The forced password-change page (FR-017) is hosted directly via
        // MainPage (see MainShellFactory.CreateForcedPasswordChange) rather than
        // a Shell route — Shell route-factory DI resolution of a page with a
        // constructor dependency proved unreliable and silently broke the flow.
    }
}
