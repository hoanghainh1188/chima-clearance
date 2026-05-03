using B2.Desktop.ViewModels.Admin;

namespace B2.Desktop.Views.Admin;

public partial class UsersPage : ContentPage
{
    public UsersPage(UsersViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is UsersViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }
}
