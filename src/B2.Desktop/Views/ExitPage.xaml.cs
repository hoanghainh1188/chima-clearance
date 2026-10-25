using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class ExitPage : ContentView
{
    public ExitPage(ExitViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
        // Wireframe § 6.4: the suffix input is focused on load and after every exit, so the
        // exit-gate officer never reaches for the mouse between trucks (#197).
        Loaded += (_, _) => FocusSuffix();
        model.ExitMarked += (_, _) => FocusSuffix();
    }

    private void FocusSuffix() => Dispatcher.Dispatch(() => SuffixEntry.Focus());
}
