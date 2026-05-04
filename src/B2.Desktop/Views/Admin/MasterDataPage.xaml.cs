using B2.Desktop.ViewModels.Admin;

namespace B2.Desktop.Views.Admin;

public partial class MasterDataPage : ContentPage
{
    public MasterDataPage(MasterDataViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MasterDataViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
