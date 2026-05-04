using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class LedgerPage : ContentPage
{
    public LedgerPage(LedgerViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is LedgerViewModel vm)
        {
            await vm.InitCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
