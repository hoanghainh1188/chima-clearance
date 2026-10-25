using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(ChangePasswordViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        // Tab order: Current → New → Confirm → Submit. .NET MAUI 8.0.100 does not
        // expose TabIndex on Entry/Button, so we rely on the natural visual-tree
        // order which already matches the required sequence.
        Loaded += OnLoaded;
    }

    // Autofocus the first field, as on LoginPage (#196).
    private void OnLoaded(object? sender, EventArgs e) =>
        Dispatcher.Dispatch(() => CurrentPasswordEntry.Focus());
}
