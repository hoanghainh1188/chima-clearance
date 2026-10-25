using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        // Tab order (wireframe § 6.1): Username → Password → Show/Hide → Login button.
        // .NET MAUI 8.0.100 does not expose TabIndex on Entry/Button (neither XAML
        // bindable nor CLR property — XFC0009 / CS1061), so we rely on the natural
        // visual-tree order which already lists the controls in the required sequence.
        Loaded += OnLoaded;
    }

    // Wireframe § 6.1: "Tên đăng nhập — Autofocus on page load" (#196). Dispatched so the
    // native control exists before focus is requested.
    private void OnLoaded(object? sender, EventArgs e) =>
        Dispatcher.Dispatch(() => UsernameEntry.Focus());

    private void OnTogglePasswordClicked(object? sender, EventArgs e) =>
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
}
