using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class KvgsPage : ContentPage
{
    public KvgsPage(KvgsViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is KvgsViewModel vm)
        {
            await vm.InitCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
