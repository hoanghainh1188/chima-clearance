using B2.Server.Api;
using B2.Server.Api.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace B2.Server.Api.Tests.Support;

/// <summary>Integration tests: isolated SQLite DB under a temp <c>B2:DataDirectory</c>.</summary>
public sealed class B2ApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "b2-api-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// F-01: stage the installer's pending initial Admin password (dev-file form) before the
    /// host starts, so <c>admin</c> logs in with <see cref="AuthHttpExtensions.InitialAdminPassword"/>.
    /// Set false to observe a fresh database with no bootstrap (seed hash retired, no login).
    /// </summary>
    public bool ProvisionAdminPassword { get; init; } = true;

    /// <summary>Temp data directory backing this factory (keys\, db\, logs\).</summary>
    public string DataDirectory => _dataDirectory;

    /// <summary>
    /// Replaces the real post-restore restarter, which would stop the in-process test host.
    /// </summary>
    public RecordingServiceRestarter Restarter { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dataDirectory);
        if (ProvisionAdminPassword)
        {
            AdminBootstrapFiles.StagePending(_dataDirectory, AuthHttpExtensions.InitialAdminPassword);
        }

        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services => services.AddSingleton<IServiceRestarter>(Restarter));
        builder.ConfigureAppConfiguration(
            (_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["B2:DataDirectory"] = _dataDirectory }));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["B2:DataDirectory"] = _dataDirectory });
        });

        return base.CreateHost(builder);
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        TryDeleteDataDir();
    }

    private void TryDeleteDataDir()
    {
        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
