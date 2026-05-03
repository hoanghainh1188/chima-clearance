using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        // Tab order: XamlC on Windows does not resolve TabIndex on Entry in XAML (XFC0009).
        UsernameEntry.TabIndex = 1;
        PasswordEntry.TabIndex = 2;
        ShowPasswordToggleButton.TabIndex = 3;
        LoginSubmitButton.TabIndex = 4;
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e) =>
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
}
