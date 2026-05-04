using B2.Desktop.ViewModels.Admin;

namespace B2.Desktop.Views.Admin;

public partial class MasterDataPage : ContentView
{
    public MasterDataPage(MasterDataViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (BindingContext is MasterDataViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
