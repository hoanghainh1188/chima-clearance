using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace B2.Desktop.WinUI;

// MAUI on Windows expects Platforms/Windows/App.xaml(.cs) — XAML compilation
// emits the static Main entry point. Sprint S0 keeps this minimal; activation,
// window chrome, and DPI scaling are tightened in S1.
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
