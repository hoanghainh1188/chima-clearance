namespace B2.Desktop;

// Sprint S0: minimal MauiApp builder so the project compiles and the MAUI
// source generators can emit the Windows entry point. UI scaffolding (Shell,
// pages, view-models, resource dictionaries) lands in S1+ per the
// implementation plan.
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        return builder.Build();
    }
}
