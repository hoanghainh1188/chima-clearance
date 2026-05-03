using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    private void OnTogglePasswordClicked(object? sender, EventArgs e) =>
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
}
