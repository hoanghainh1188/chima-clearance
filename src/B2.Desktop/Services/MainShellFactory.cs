namespace B2.Desktop.Services;

public sealed class MainShellFactory : IMainShellFactory
{
    private readonly IServiceProvider _services;

    public MainShellFactory(IServiceProvider services) => _services = services;

    public Page CreateMainShell() => new MainShellPage(_services);
}
