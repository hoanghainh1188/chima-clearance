using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Desktop.Views;
using B2.Desktop.Views.Admin;
using B2.Shared.Contracts.Users;

namespace B2.Desktop;

/// <summary>
/// Post-login shell. ContentPage + Grid layout per wireframe § 5: top bar
/// (title + tri-state connectivity badge + user menu), nav strip, content
/// host, status bar. Identity and role come from <see cref="ISessionContext"/>;
/// the connectivity badge tracks <see cref="ConnectivityService.Mode"/>; the
/// status bar binds to <see cref="ISessionCorrelationService.CurrentCorrelationId"/>.
/// </summary>
public sealed class MainShellPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly AuthTokenHandler _auth;
    private readonly ISessionContext _session;
    private readonly ConnectivityService _connectivity;
    private readonly UserRoleDto _role;
    private readonly ContentView _contentHost = new();
    private readonly List<TabEntry> _tabs = new();
    private readonly Border _connectivityBadge;
    private readonly Label _connectivityLabel;
    private TabEntry? _active;

    public MainShellPage(IServiceProvider services)
    {
        _services = services;
        _auth = services.GetRequiredService<AuthTokenHandler>();
        _session = services.GetRequiredService<ISessionContext>();
        _connectivity = services.GetRequiredService<ConnectivityService>();
        _role = _session.Role;

        Title = AppResources.Shell_AppTitle;
        BackgroundColor = (Color)Application.Current!.Resources["ColorSurface"];

        _connectivityBadge = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(10, 4),
            VerticalOptions = LayoutOptions.Center,
        };
        _connectivityLabel = new Label
        {
            VerticalOptions = LayoutOptions.Center,
        };
        _connectivityBadge.Content = _connectivityLabel;

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

        var topBar = BuildTopBar();
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

        _connectivity.ModeChanged += OnConnectivityModeChanged;
        UpdateConnectivityBadge(_connectivity.Mode);
        _connectivity.Start();

        if (_tabs.Count > 0)
        {
            ActivateTab(_tabs[0]);
        }
    }

    private View BuildTopBar()
    {
        var bar = new Grid
        {
            BackgroundColor = (Color)Application.Current!.Resources["ColorTopBarBg"],
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = 16,
            Padding = new Thickness(16, 8),
        };

        var titleLabel = new Label
        {
            Text = AppResources.Shell_AppTitle,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current.Resources["ColorPrimary"],
            VerticalOptions = LayoutOptions.Center,
        };
        var titleTap = new TapGestureRecognizer();
        titleTap.Tapped += (_, _) =>
        {
            if (_tabs.Count > 0)
            {
                ActivateTab(_tabs[0]);
            }
        };
        titleLabel.GestureRecognizers.Add(titleTap);
        Grid.SetColumn(titleLabel, 0);
        bar.Children.Add(titleLabel);

        Grid.SetColumn(_connectivityBadge, 1);
        bar.Children.Add(_connectivityBadge);

        var userMenu = new Button
        {
            BackgroundColor = Colors.Transparent,
            TextColor = (Color)Application.Current.Resources["ColorOnSurface"],
        };
        userMenu.SetBinding(
            Button.TextProperty,
            new Binding(
                nameof(ISessionContext.FullName),
                source: _session,
                stringFormat: "{0}" + AppResources.UserMenu_Suffix));
        userMenu.Clicked += OnUserMenuClicked;
        Grid.SetColumn(userMenu, 2);
        bar.Children.Add(userMenu);

        return bar;
    }

    private View BuildNavStrip()
    {
        var strip = new HorizontalStackLayout
        {
            BackgroundColor = (Color)Application.Current!.Resources["ColorTopBarBg"],
            Spacing = 4,
            Padding = new Thickness(8, 4),
        };

        AddTab(strip, AppResources.Ledger_PageTitle, _services.GetRequiredService<LedgerPage>());

        if (_role is UserRoleDto.Admin or UserRoleDto.Operator)
        {
            AddTab(strip, AppResources.Entry_PageTitle, _services.GetRequiredService<EntryPage>());
            AddTab(strip, AppResources.Exit_PageTitle, _services.GetRequiredService<ExitPage>());
        }

        if (_role is UserRoleDto.Admin or UserRoleDto.Supervisor)
        {
            AddTab(strip, AppResources.Kvgs_PageTitle, _services.GetRequiredService<KvgsPage>());
        }

        if (_role == UserRoleDto.Admin)
        {
            AddTab(strip, AppResources.MasterData_PageTitle, _services.GetRequiredService<MasterDataPage>());
            AddTab(strip, AppResources.Users_PageTitle, _services.GetRequiredService<UsersPage>());
        }

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

    private void OnConnectivityModeChanged(object? sender, EventArgs e)
    {
        var mode = _connectivity.Mode;
        MainThread.BeginInvokeOnMainThread(() => UpdateConnectivityBadge(mode));
    }

    private void UpdateConnectivityBadge(ConnectivityMode mode)
    {
        var resources = Application.Current!.Resources;
        switch (mode)
        {
            case ConnectivityMode.Online:
                _connectivityBadge.BackgroundColor = (Color)resources["ColorBadgeOnlineBg"];
                _connectivityLabel.TextColor = (Color)resources["ColorBadgeOnlineText"];
                _connectivityLabel.Text = AppResources.TopBar_Badge_Online;
                break;
            case ConnectivityMode.PollingFallback:
                _connectivityBadge.BackgroundColor = (Color)resources["ColorBadgePollingBg"];
                _connectivityLabel.TextColor = (Color)resources["ColorBadgePollingText"];
                _connectivityLabel.Text = AppResources.TopBar_Badge_Polling;
                break;
            default:
                _connectivityBadge.BackgroundColor = (Color)resources["ColorBadgeOfflineBg"];
                _connectivityLabel.TextColor = (Color)resources["ColorBadgeOfflineText"];
                _connectivityLabel.Text = AppResources.TopBar_Badge_Offline;
                break;
        }
    }

    private async void OnUserMenuClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet(
            title: null,
            cancel: AppResources.Common_Cancel,
            destruction: null,
            AppResources.Common_Logout).ConfigureAwait(true);
        if (string.Equals(choice, AppResources.Common_Logout, StringComparison.Ordinal))
        {
            await LogoutAsync().ConfigureAwait(false);
        }
    }

    private async Task LogoutAsync()
    {
        _connectivity.ModeChanged -= OnConnectivityModeChanged;
        await _connectivity.StopAsync().ConfigureAwait(false);
        _auth.AccessToken = null;
        _session.Clear();
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Application.Current!.MainPage = _services.GetRequiredService<AppShell>();
        }).ConfigureAwait(false);
    }

    private sealed record TabEntry(Button Button, ContentView View);
}
