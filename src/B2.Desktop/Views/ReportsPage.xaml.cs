using B2.Desktop.ViewModels;

namespace B2.Desktop.Views;

public partial class ReportsPage : ContentView
{
    public ReportsPage(ReportsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        Loaded += async (_, _) => await vm.LoadCommand.ExecuteAsync(null);
    }
}
