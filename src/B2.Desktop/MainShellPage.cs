using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Desktop.Views;
using B2.Desktop.Views.Admin;
using B2.Shared.Contracts.Users;

namespace B2.Desktop;

/// <summary>
/// Post-login shell. ContentPage + Grid layout per wireframe § 5: top bar
/// (commit #2 placeholder for now), nav strip, content host, status bar.
/// Status bar surfaces <see cref="ISessionCorrelationService.CurrentCorrelationId"/>
/// as "Mã phiên" so operators can quote it to support.
/// </summary>
public sealed class MainShellPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly AuthTokenHandler _auth;
    private readonly UserRoleDto _role;
    private readonly ContentView _contentHost = new();
    private readonly List<TabEntry> _tabs = new();
    private TabEntry? _active;

    public MainShellPage(IServiceProvider services, UserRoleDto role)
    {
        _services = services;
        _auth = services.GetRequiredService<AuthTokenHandler>();
        _role = role;

        Title = AppResources.Shell_AppTitle;
        BackgroundColor = (Color)Application.Current!.Resources["ColorSurface"];

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(56) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = new GridLength(32) },
            },
        };

        var topBar = new Border
        {
            BackgroundColor = (Color)Application.Current.Resources["ColorTopBarBg"],
            StrokeThickness = 0,
        };
        Grid.SetRow(topBar, 0);
        grid.Children.Add(topBar);

        var navStrip = BuildNavStrip();
        Grid.SetRow(navStrip, 1);
        grid.Children.Add(navStrip);

        Grid.SetRow(_contentHost, 2);
        grid.Children.Add(_contentHost);

        var statusBar = BuildStatusBar(services.GetRequiredService<ISessionCorrelationService>());
        Grid.SetRow(statusBar, 3);
        grid.Children.Add(statusBar);

        Content = grid;

        services.GetRequiredService<ConnectivityService>().Start();

        if (_tabs.Count > 0)
        {
            ActivateTab(_tabs[0]);
        }
    }

    private View BuildNavStrip()
    {
        var strip = new Grid
        {
            BackgroundColor = (Color)Application.Current!.Resources["ColorTopBarBg"],
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Padding = new Thickness(8, 4),
        };

        var tabs = new HorizontalStackLayout { Spacing = 4 };
        Grid.SetColumn(tabs, 0);
        strip.Children.Add(tabs);

        AddTab(tabs, AppResources.Ledger_PageTitle, _services.GetRequiredService<LedgerPage>());

        if (_role is UserRoleDto.Admin or UserRoleDto.Operator)
        {
            AddTab(tabs, AppResources.Entry_PageTitle, _services.GetRequiredService<EntryPage>());
            AddTab(tabs, AppResources.Exit_PageTitle, _services.GetRequiredService<ExitPage>());
        }

        if (_role is UserRoleDto.Admin or UserRoleDto.Supervisor)
        {
            AddTab(tabs, AppResources.Kvgs_PageTitle, _services.GetRequiredService<KvgsPage>());
        }

        if (_role == UserRoleDto.Admin)
        {
            AddTab(tabs, AppResources.MasterData_PageTitle, _services.GetRequiredService<MasterDataPage>());
            AddTab(tabs, AppResources.Users_PageTitle, _services.GetRequiredService<UsersPage>());
        }

        var logout = new Button
        {
            Text = AppResources.Common_Logout,
            BackgroundColor = Colors.Transparent,
            TextColor = (Color)Application.Current.Resources["ColorOnSurface"],
        };
        logout.Clicked += OnLogoutClicked;
        Grid.SetColumn(logout, 1);
        strip.Children.Add(logout);

        return strip;
    }

    private void AddTab(HorizontalStackLayout host, string title, ContentView view)
    {
        var button = new Button
        {
            Text = title,
            BackgroundColor = Colors.Transparent,
            TextColor = (Color)Application.Current!.Resources["ColorOnSurface"],
        };
        var entry = new TabEntry(button, view);
        button.Clicked += (_, _) => ActivateTab(entry);
        host.Children.Add(button);
        _tabs.Add(entry);
    }

    private void ActivateTab(TabEntry tab)
    {
        if (_active is not null && ReferenceEquals(_active.Button, tab.Button))
        {
            return;
        }

        _contentHost.Content = tab.View;

        var primary = (Color)Application.Current!.Resources["ColorPrimary"];
        var muted = (Color)Application.Current.Resources["ColorOnSurface"];
        foreach (var entry in _tabs)
        {
            var isActive = ReferenceEquals(entry.Button, tab.Button);
            entry.Button.TextColor = isActive ? primary : muted;
            entry.Button.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
        }

        _active = tab;
    }

    private static View BuildStatusBar(ISessionCorrelationService correlation)
    {
        var bar = new Grid
        {
            BackgroundColor = (Color)Application.Current!.Resources["ColorStatusBarBg"],
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = 16,
            Padding = new Thickness(12, 4),
        };

        var muted = (Color)Application.Current.Resources["ColorOnSurfaceMuted"];

        var fontSize = new Label
        {
            Text = string.Concat(
                AppResources.Status_FontSize_Label,
                " ",
                AppResources.Status_FontSize_Normal),
            VerticalOptions = LayoutOptions.Center,
            TextColor = muted,
        };
        Grid.SetColumn(fontSize, 0);
        bar.Children.Add(fontSize);

        var shortcuts = new Label
        {
            Text = AppResources.Status_Shortcuts_F1,
            VerticalOptions = LayoutOptions.Center,
            TextColor = muted,
        };
        Grid.SetColumn(shortcuts, 1);
        bar.Children.Add(shortcuts);

        var fallback = string.Concat(
            AppResources.Status_Session_Label,
            " ",
            AppResources.Status_Session_None);
        var session = new Label
        {
            VerticalOptions = LayoutOptions.Center,
            TextColor = muted,
        };
        session.SetBinding(
            Label.TextProperty,
            new Binding(
                nameof(ISessionCorrelationService.CurrentCorrelationId),
                source: correlation,
                stringFormat: AppResources.Status_Session_Label + " {0}",
                fallbackValue: fallback,
                targetNullValue: fallback));
        Grid.SetColumn(session, 2);
        bar.Children.Add(session);

        var version = new Label
        {
            Text = AppResources.Status_AppVersion,
            VerticalOptions = LayoutOptions.Center,
            TextColor = muted,
        };
        Grid.SetColumn(version, 3);
        bar.Children.Add(version);

        return bar;
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        var connectivity = _services.GetRequiredService<ConnectivityService>();
        await connectivity.StopAsync().ConfigureAwait(false);
        _auth.AccessToken = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Application.Current!.MainPage = _services.GetRequiredService<AppShell>();
        }).ConfigureAwait(false);
    }

    private sealed record TabEntry(Button Button, ContentView View);
}
