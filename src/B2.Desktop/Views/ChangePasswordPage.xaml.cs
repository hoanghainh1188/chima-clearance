using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(ChangePasswordViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        CurrentPasswordEntry.TabIndex = 1;
        NewPasswordEntry.TabIndex = 2;
        ConfirmPasswordEntry.TabIndex = 3;
        ChangePasswordSubmitButton.TabIndex = 4;
    }
}
