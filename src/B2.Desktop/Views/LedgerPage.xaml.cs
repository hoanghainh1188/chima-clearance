using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class LedgerPage : ContentView
{
    // VK_OEM_2 (0xBF) is the "/" key on US/QWERTY-derived layouts; Windows.System.VirtualKey has no named member for it.
    private const int VirtualKeyForwardSlash = 0xBF;

    private bool _acceleratorsRegistered;

    public LedgerPage(LedgerViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (BindingContext is LedgerViewModel vm)
        {
            await vm.InitCommand.ExecuteAsync(null).ConfigureAwait(true);
        }

        if (!_acceleratorsRegistered)
        {
            RegisterWindowsAccelerators();
            _acceleratorsRegistered = true;
        }
    }

    /// <summary>
    /// Wireframe § 6.2 in-list shortcuts: <c>F5</c> manual refresh,
    /// <c>/</c> focuses the suffix-search Entry, <c>Esc</c> closes the
    /// detail drawer (if open) or clears the search text.
    /// </summary>
    private void RegisterWindowsAccelerators()
    {
        if (Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement element)
        {
            return;
        }

        AttachAccelerator(
            element,
            Windows.System.VirtualKey.F5,
            Windows.System.VirtualKeyModifiers.None,
            () =>
            {
                if (BindingContext is LedgerViewModel vm && vm.RefreshCommand.CanExecute(null))
                {
                    _ = vm.RefreshCommand.ExecuteAsync(null);
                }

                return true;
            });

        AttachAccelerator(
            element,
            (Windows.System.VirtualKey)VirtualKeyForwardSlash,
            Windows.System.VirtualKeyModifiers.None,
            () =>
            {
                // "/" is an ordinary character inside a text field (dates 23/10/2026, "cont/mooc"):
                // leave it unhandled there so the field receives it (#190).
                if (IsTextInputFocused(element))
                {
                    return false;
                }

                PlateSearchEntry?.Focus();
                return true;
            });

        AttachAccelerator(
            element,
            Windows.System.VirtualKey.Escape,
            Windows.System.VirtualKeyModifiers.None,
            () =>
            {
                if (BindingContext is not LedgerViewModel vm)
                {
                    return true;
                }

                if (vm.IsDrawerOpen)
                {
                    vm.CloseDetailCommand.Execute(null);
                    return true;
                }

                vm.PlateSearch = string.Empty;
                return true;
            });
    }

    private static bool IsTextInputFocused(Microsoft.UI.Xaml.UIElement element) =>
        element.XamlRoot is { } root
        && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) is
            Microsoft.UI.Xaml.Controls.TextBox
            or Microsoft.UI.Xaml.Controls.PasswordBox
            or Microsoft.UI.Xaml.Controls.RichEditBox
            or Microsoft.UI.Xaml.Controls.AutoSuggestBox;

    private static void AttachAccelerator(
        Microsoft.UI.Xaml.UIElement element,
        Windows.System.VirtualKey key,
        Windows.System.VirtualKeyModifiers modifiers,
        Func<bool> invoke)
    {
        var accelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = key,
            Modifiers = modifiers,
        };
        accelerator.Invoked += (_, args) =>
        {
            // false = not ours here: let the key reach the focused control.
            args.Handled = invoke();
        };
        element.KeyboardAccelerators.Add(accelerator);
    }
}
