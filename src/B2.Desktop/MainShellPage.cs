using B2.Desktop.Resources.Strings;
using B2.Desktop.Services;
using B2.Desktop.Services.Api;
using B2.Desktop.Services.UserSettings;
using B2.Desktop.Views;
using B2.Desktop.Views.Admin;
using B2.Shared.Contracts.Users;
using Microsoft.Maui.Controls.Shapes;

namespace B2.Desktop;

/// <summary>
/// Post-login shell. ContentPage + Grid layout per wireframe § 5: top bar
/// (title + tri-state badge + user menu), vertical sidebar (220 px) on the
/// left, content host on the right, status bar across the bottom.
///
/// Identity and role come from <see cref="ISessionContext"/>; the
/// connectivity badge tracks <see cref="ConnectivityService.Mode"/>; the
/// status bar binds to <see cref="ISessionCorrelationService.CurrentCorrelationId"/>.
/// Platform-specific code (Windows accelerators) calls
/// <see cref="ActivateTabByIndex(int)"/> and <see cref="ShowShortcutsAsync"/>
/// to wire <c>Alt+1..8</c> and <c>F1</c> respectively.
/// </summary>
public sealed class MainShellPage : ContentPage, IToastPresenter
{
    private readonly VerticalStackLayout _toastHost = new()
    {
        Spacing = 8,
        VerticalOptions = LayoutOptions.End,
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, 0, 0, 52),
        // Pass clicks through the empty overlay, but let the toasts themselves
        // remain tappable (tap-to-dismiss).
        InputTransparent = true,
        CascadeInputTransparent = false,
    };

    private readonly IServiceProvider _services;
    private readonly IAuthTokenStore _auth;
    private readonly ISessionContext _session;
    private readonly ConnectivityService _connectivity;
    private readonly IModalService _modal;
    private readonly IAppNavigator _navigator;
    private readonly IToastService _toast;
    private readonly IUserSettingsService _userSettings;
    private readonly UserRoleDto _role;
    private readonly ContentView _contentHost = new();
    private readonly List<TabEntry> _tabs = new();
    private readonly Border _connectivityBadge;
    private readonly Label _connectivityLabel;
    private TabEntry? _active;

    public MainShellPage(IServiceProvider services)
    {
        _services = services;
        _auth = services.GetRequiredService<IAuthTokenStore>();
        _session = services.GetRequiredService<ISessionContext>();
        _connectivity = services.GetRequiredService<ConnectivityService>();
        _modal = services.GetRequiredService<IModalService>();
        _navigator = services.GetRequiredService<IAppNavigator>();
        _toast = services.GetRequiredService<IToastService>();
        _userSettings = services.GetRequiredService<IUserSettingsService>();
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
        var badgeTap = new TapGestureRecognizer();
        badgeTap.Tapped += OnConnectivityBadgeTapped;
        _connectivityBadge.GestureRecognizers.Add(badgeTap);

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(56) },
                new RowDefinition { Height = GridLength.Star },
                // 44 px (was 32): the Cỡ chữ Picker / ComboBox needs ~36 px of
                // intrinsic height; a 32 px row clipped its selected-item text.
                new RowDefinition { Height = new GridLength(44) },
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(220) },
                new ColumnDefinition { Width = GridLength.Star },
            },
        };

        var topBar = BuildTopBar();
        Grid.SetRow(topBar, 0);
        Grid.SetColumn(topBar, 0);
        Grid.SetColumnSpan(topBar, 2);
        grid.Children.Add(topBar);

        var sidebar = BuildSidebar();
        Grid.SetRow(sidebar, 1);
        Grid.SetColumn(sidebar, 0);
        grid.Children.Add(sidebar);

        Grid.SetRow(_contentHost, 1);
        Grid.SetColumn(_contentHost, 1);
        grid.Children.Add(_contentHost);

        var statusBar = BuildStatusBar(services.GetRequiredService<ISessionCorrelationService>());
        Grid.SetRow(statusBar, 2);
        Grid.SetColumn(statusBar, 0);
        Grid.SetColumnSpan(statusBar, 2);
        grid.Children.Add(statusBar);

        // Toast overlay spans the whole shell and sits on top (added last).
        Grid.SetRow(_toastHost, 0);
        Grid.SetRowSpan(_toastHost, 3);
        Grid.SetColumn(_toastHost, 0);
        Grid.SetColumnSpan(_toastHost, 2);
        grid.Children.Add(_toastHost);

        Content = grid;

        _connectivity.ModeChanged += OnConnectivityModeChanged;
        UpdateConnectivityBadge(_connectivity.Mode);
        _connectivity.Start();

        if (_tabs.Count > 0)
        {
            ActivateTab(_tabs[0]);
        }

        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        RegisterWindowsAccelerators();
    }

    /// <summary>
    /// Wire the <c>F1</c> and <c>Alt+1..Alt+8</c> Windows keyboard
    /// accelerators per wireframe § 5. Invoked on first <c>Loaded</c> when
    /// the page handler has been mounted on a <c>Microsoft.UI.Xaml.UIElement</c>.
    /// </summary>
    private void RegisterWindowsAccelerators()
    {
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement element)
        {
            return;
        }

        AttachAccelerator(
            element,
            Windows.System.VirtualKey.F1,
            Windows.System.VirtualKeyModifiers.None,
            () => _ = ShowShortcutsAsync());

        for (var i = 1; i <= 8; i++)
        {
            var captured = i;
            var key = (Windows.System.VirtualKey)((int)Windows.System.VirtualKey.Number0 + captured);
            AttachAccelerator(
                element,
                key,
                Windows.System.VirtualKeyModifiers.Menu,
                () => ActivateTabByIndex(captured));
        }
    }

    private static void AttachAccelerator(
        Microsoft.UI.Xaml.UIElement element,
        Windows.System.VirtualKey key,
        Windows.System.VirtualKeyModifiers modifiers,
        Action invoke)
    {
        var accelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers,
        };
        accelerator.Invoked += (_, args) =>
        {
            invoke();
            args.Handled = true;
        };
        element.KeyboardAccelerators.Add(accelerator);
    }

    /// <summary>
    /// Activate the sidebar item at <paramref name="oneBasedIndex"/>. Used by
    /// Windows <c>Alt+N</c> accelerators registered in <c>Platforms/Windows</c>.
    /// Out-of-range indices are no-ops.
    /// </summary>
    public void ActivateTabByIndex(int oneBasedIndex)
    {
        var idx = oneBasedIndex - 1;
        if (idx < 0 || idx >= _tabs.Count)
        {
            return;
        }

        ActivateTab(_tabs[idx]);
    }

    /// <summary>
    /// Open the F1 cheat-sheet modal. Public so platform-specific accelerator
    /// code can invoke it on the keypress.
    /// </summary>
    public Task ShowShortcutsAsync()
    {
        return _modal.ShowAsync(
            AppResources.Shortcuts_Title,
            AppResources.Shortcuts_Body,
            AppResources.Common_Close);
    }

    private Grid BuildTopBar()
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

    private Grid BuildSidebar()
    {
        var container = new Grid
        {
            BackgroundColor = (Color)Application.Current!.Resources["ColorSidebarBg"],
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
            },
            Padding = new Thickness(0, 8),
        };

        var stack = new VerticalStackLayout { Spacing = 2 };

        AddSidebarItem(
            stack,
            AppResources.Sidebar_Item_Ledger,
            _services.GetRequiredService<LedgerPage>());

        if (_role is UserRoleDto.Admin or UserRoleDto.Operator)
        {
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Entry,
                _services.GetRequiredService<EntryPage>());
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Exit,
                _services.GetRequiredService<ExitPage>());
        }

        // KVGS is an exit-gate column (business rules § B.2): the officer who ticks "xe ra"
        // ticks it too, so it is visible to every role, not just Supervisor.
        AddSidebarItem(
            stack,
            AppResources.Sidebar_Item_Kvgs,
            _services.GetRequiredService<KvgsPage>());

        if (_role is UserRoleDto.Admin or UserRoleDto.Supervisor)
        {
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Reports,
                _services.GetRequiredService<ReportsPage>());
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Export,
                _services.GetRequiredService<ExportPage>());
        }

        if (_role == UserRoleDto.Admin)
        {
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_MasterData,
                _services.GetRequiredService<MasterDataPage>());
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Users,
                _services.GetRequiredService<UsersPage>());
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Audit,
                _services.GetRequiredService<AuditPage>());
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Health,
                _services.GetRequiredService<HealthPage>());
            AddSidebarItem(
                stack,
                AppResources.Sidebar_Item_Backup,
                _services.GetRequiredService<BackupPage>());
        }

        container.Children.Add(stack);
        return container;
    }

    private void AddSidebarItem(VerticalStackLayout host, string label, ContentView view)
    {
        var item = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(4) },
                new ColumnDefinition { Width = GridLength.Star },
            },
            HeightRequest = 44,
            BackgroundColor = Colors.Transparent,
        };

        var leftBar = new BoxView
        {
            Color = (Color)Application.Current!.Resources["ColorPrimary"],
            IsVisible = false,
        };
        Grid.SetColumn(leftBar, 0);
        item.Children.Add(leftBar);

        var titleLabel = new Label
        {
            Text = label,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(12, 0, 12, 0),
            TextColor = (Color)Application.Current.Resources["ColorOnSurface"],
        };
        Grid.SetColumn(titleLabel, 1);
        item.Children.Add(titleLabel);

        var entry = new TabEntry(item, leftBar, titleLabel, view);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => ActivateTab(entry);
        item.GestureRecognizers.Add(tap);

        host.Children.Add(item);
        _tabs.Add(entry);
    }

    private void ActivateTab(TabEntry tab)
    {
        if (_active is not null && ReferenceEquals(_active.Container, tab.Container))
        {
            return;
        }

        _contentHost.Content = tab.View;

        foreach (var entry in _tabs)
        {
            var isActive = ReferenceEquals(entry.Container, tab.Container);
            entry.LeftBar.IsVisible = isActive;
            entry.TitleLabel.FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None;
        }

        _active = tab;
    }

    private Grid BuildStatusBar(ISessionCorrelationService correlation)
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
            // Slim vertical padding so the Cỡ chữ Picker gets the full row
            // height (44 − 4 = 40 px) and its text is not clipped.
            Padding = new Thickness(12, 2),
        };

        var muted = (Color)Application.Current.Resources["ColorOnSurfaceMuted"];

        var fontSize = BuildFontSizePicker(muted);
        Grid.SetColumn(fontSize, 0);
        bar.Children.Add(fontSize);

        var shortcuts = new Label
        {
            Text = AppResources.Status_Shortcuts_F1,
            VerticalOptions = LayoutOptions.Center,
            TextColor = muted,
            TextDecorations = TextDecorations.Underline,
        };
        var shortcutsTap = new TapGestureRecognizer();
        shortcutsTap.Tapped += OnShortcutsTapped;
        shortcuts.GestureRecognizers.Add(shortcutsTap);
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
                stringFormat: AppResources.Status_Session_Label + " {0}")
            {
                FallbackValue = fallback,
                TargetNullValue = fallback,
            });
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

    private HorizontalStackLayout BuildFontSizePicker(Color textColor)
    {
        var layout = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
        };

        var label = new Label
        {
            Text = AppResources.Status_FontSize_Label,
            VerticalOptions = LayoutOptions.Center,
            TextColor = textColor,
        };
        layout.Children.Add(label);

        var picker = new Picker
        {
            VerticalOptions = LayoutOptions.Center,
            // Pin the height so the WinUI ComboBox renders its selected text in
            // full inside the compact status bar instead of clipping it.
            HeightRequest = 36,
            MinimumHeightRequest = 36,
            TextColor = textColor,
            BackgroundColor = Colors.Transparent,
        };
        picker.Items.Add(AppResources.Status_FontSize_Normal);
        picker.Items.Add(AppResources.Status_FontSize_Large);
        picker.Items.Add(AppResources.Status_FontSize_ExtraLarge);

        picker.SelectedIndex = (int)_userSettings.GetFontScale();
        picker.SelectedIndexChanged += OnFontScalePickerChanged;
        layout.Children.Add(picker);

        return layout;
    }

    private async void OnFontScalePickerChanged(object? sender, EventArgs e)
    {
        if (sender is not Picker picker)
        {
            return;
        }

        var index = picker.SelectedIndex;
        if (index < 0 || index > (int)FontScaleMode.ExtraLarge)
        {
            return;
        }

        var newMode = (FontScaleMode)index;
        var current = _userSettings.GetFontScale();
        if (newMode == current)
        {
            return;
        }

        _userSettings.SetFontScale(newMode);
        await _toast.ShowAsync(
            AppResources.Status_FontSize_RestartHint,
            ToastSeverity.Warning).ConfigureAwait(true);
    }

    private async void OnConnectivityBadgeTapped(object? sender, TappedEventArgs e)
    {
        var detail = _connectivity.Mode switch
        {
            ConnectivityMode.Online => AppResources.Connectivity_Detail_Online,
            ConnectivityMode.PollingFallback => AppResources.Connectivity_Detail_Polling,
            _ => AppResources.Connectivity_Detail_Offline,
        };

        await _modal.ShowAsync(
            AppResources.Connectivity_Detail_Title,
            detail,
            AppResources.Common_Close).ConfigureAwait(true);
    }

    private async void OnUserMenuClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet(
            title: null,
            cancel: AppResources.Common_Cancel,
            destruction: null,
            AppResources.UserMenu_ChangePassword,
            AppResources.Common_Logout).ConfigureAwait(true);
        if (string.Equals(choice, AppResources.Common_Logout, StringComparison.Ordinal))
        {
            await LogoutAsync().ConfigureAwait(false);
            return;
        }

        if (string.Equals(choice, AppResources.UserMenu_ChangePassword, StringComparison.Ordinal))
        {
            await PushChangePasswordModalAsync().ConfigureAwait(true);
        }
    }

    private async Task PushChangePasswordModalAsync()
    {
        var page = _services.GetRequiredService<Views.ChangePasswordPage>();
        if (page.BindingContext is ViewModels.ChangePasswordViewModel vm)
        {
            vm.CanCancel = true;
        }

        await Navigation.PushModalAsync(page).ConfigureAwait(true);
    }

    private async void OnShortcutsTapped(object? sender, TappedEventArgs e)
    {
        await ShowShortcutsAsync().ConfigureAwait(true);
    }

    private async Task LogoutAsync()
    {
        _connectivity.ModeChanged -= OnConnectivityModeChanged;
        await _connectivity.StopAsync().ConfigureAwait(false);
        _auth.AccessToken = null;
        _session.Clear();
        await _navigator.NavigateToLoginAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// <see cref="IToastPresenter"/>: render an in-app toast in the overlay.
    /// Severity maps to a solid background token (icon + colour + text per
    /// Constitution VI / WCAG 1.4.1). Always invoked on the UI thread by
    /// <see cref="ToastService"/>.
    /// </summary>
    public void ShowToast(string message, ToastSeverity severity, TimeSpan duration)
    {
        var resources = Application.Current!.Resources;
        var background = (Color)(severity switch
        {
            ToastSeverity.Success => resources["ColorLedgerStatusAllExited"],
            ToastSeverity.Warning => resources["ColorWarning"],
            ToastSeverity.Error => resources["ColorError"],
            _ => resources["ColorPrimary"],
        });
        var glyph = severity switch
        {
            ToastSeverity.Success => "✓  ",
            ToastSeverity.Warning => "⚠  ",
            ToastSeverity.Error => "✕  ",
            _ => "ℹ  ",
        };

        var border = new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            Padding = new Thickness(16, 10),
            Opacity = 0,
            MaximumWidthRequest = 520,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Content = new Label
            {
                Text = glyph + message,
                TextColor = Colors.White,
                FontSize = (double)resources["FontSizeM"],
                LineBreakMode = LineBreakMode.WordWrap,
            },
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => RemoveToast(border);
        border.GestureRecognizers.Add(tap);

        _toastHost.Add(border);
        _ = AnimateToastAsync(border, duration);
    }

    private async Task AnimateToastAsync(Border border, TimeSpan duration)
    {
        await border.FadeTo(1, 150).ConfigureAwait(true);
        await Task.Delay(duration).ConfigureAwait(true);
        if (_toastHost.Contains(border))
        {
            await border.FadeTo(0, 250).ConfigureAwait(true);
            RemoveToast(border);
        }
    }

    private void RemoveToast(Border border)
    {
        if (_toastHost.Contains(border))
        {
            _toastHost.Remove(border);
        }
    }

    private sealed record TabEntry(Grid Container, BoxView LeftBar, Label TitleLabel, ContentView View);
}
