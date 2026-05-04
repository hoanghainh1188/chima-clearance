using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class LedgerPage : ContentView
{
    public LedgerPage(LedgerViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (BindingContext is LedgerViewModel vm)
        {
            await vm.InitCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
