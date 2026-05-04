using B2.Desktop.ViewModels.Admin;

namespace B2.Desktop.Views.Admin;

public partial class UsersPage : ContentView
{
    public UsersPage(UsersViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (BindingContext is UsersViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
