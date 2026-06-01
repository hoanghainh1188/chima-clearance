using B2.Desktop.ViewModels;

namespace B2.Desktop.Views.Admin;

public partial class BackupPage : ContentView
{
    public BackupPage(BackupViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
