using B2.Desktop.ViewModels;

namespace B2.Desktop.Views.Admin;

public partial class AuditPage : ContentView
{
    public AuditPage(AuditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        Loaded += async (_, _) => await vm.LoadCommand.ExecuteAsync(null);
    }
}
