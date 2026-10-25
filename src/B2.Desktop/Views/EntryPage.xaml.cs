using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class EntryPage : ContentView
{
    public EntryPage(EntryViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Loaded += OnLoaded;
        model.FormCleared += (_, _) => FocusFirstField();
    }

    // The shell keeps one EntryPage for the whole session and re-attaches it on every sidebar
    // click, so Loaded fires each time the tab is shown. Staying subscribed re-reads the
    // processor/yard lists, so one just added under "Danh mục" is offered without a restart.
    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (BindingContext is EntryViewModel vm)
        {
            await vm.InitCommand.ExecuteAsync(null).ConfigureAwait(true);
        }

        FocusFirstField();
    }

    // #198: focus the first field to fill — the declaration number on a fresh form, the plate
    // while adding further vehicles to the same declaration (its section is read-only then).
    private void FocusFirstField() => Dispatcher.Dispatch(() =>
    {
        if (BindingContext is EntryViewModel { IsAddChildMode: true })
        {
            PlateEntry.Focus();
        }
        else
        {
            DeclarationEditor.Focus();
        }
    });
}
