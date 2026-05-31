using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class ExportPage : ContentView
{
    public ExportPage(ExportViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
