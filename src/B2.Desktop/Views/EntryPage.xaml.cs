using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class EntryPage : ContentView
{
    public EntryPage(EntryViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        if (BindingContext is EntryViewModel vm)
        {
            await vm.InitCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }
}
