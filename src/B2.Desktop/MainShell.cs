using B2.Desktop.Resources.Strings;
using B2.Desktop.Services.Api;
using B2.Desktop.Views;
using B2.Desktop.Views.Admin;
using B2.Shared.Contracts.Users;

namespace B2.Desktop;

/// <summary>
/// Post-login shell: tab bar by role (wireframe § 6 sidebar matrix).
/// </summary>
public sealed class MainShell : Shell
{
    private readonly IServiceProvider _services;
    private readonly AuthTokenHandler _auth;

    public MainShell(IServiceProvider services, UserRoleDto role)
    {
        _services = services;
        _auth = services.GetRequiredService<AuthTokenHandler>();
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Title = AppResources.Shell_AppTitle;

        var bar = new TabBar();
        if (role is UserRoleDto.Admin or UserRoleDto.Operator)
        {
            bar.Items.Add(
                new ShellContent
                {
                    Title = AppResources.Entry_PageTitle,
                    Route = "entry",
                    Content = services.GetRequiredService<EntryPage>(),
                });
            bar.Items.Add(
                new ShellContent
                {
                    Title = AppResources.Exit_PageTitle,
                    Route = "exit",
                    Content = services.GetRequiredService<ExitPage>(),
                });
        }

        if (role == UserRoleDto.Admin)
        {
            bar.Items.Add(
                new ShellContent
                {
                    Title = AppResources.MasterData_PageTitle,
                    Route = "master",
                    Content = services.GetRequiredService<MasterDataPage>(),
                });
            bar.Items.Add(
                new ShellContent
                {
                    Title = AppResources.Users_PageTitle,
                    Route = "users",
                    Content = services.GetRequiredService<UsersPage>(),
                });
        }

        if (role == UserRoleDto.Supervisor)
        {
            bar.Items.Add(
                new ShellContent
                {
                    Title = AppResources.Supervisor_HomeTitle,
                    Route = "supervisor",
                    Content = services.GetRequiredService<SupervisorHomePage>(),
                });
        }

        Items.Add(bar);

        ToolbarItems.Add(
            new ToolbarItem
            {
                Text = AppResources.Common_Logout,
                Priority = 0,
                Command = new Command(() => _ = LogoutAsync()),
            });
    }

    private async Task LogoutAsync()
    {
        _auth.AccessToken = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Application.Current!.MainPage = _services.GetRequiredService<AppShell>();
        }).ConfigureAwait(false);
    }
}
