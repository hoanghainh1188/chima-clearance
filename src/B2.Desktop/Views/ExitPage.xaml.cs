using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class ExitPage : ContentView
{
    public ExitPage(ExitViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
