using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class EntryPage : ContentPage
{
    public EntryPage(EntryViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EntryViewModel vm)
        {
            await vm.InitCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
