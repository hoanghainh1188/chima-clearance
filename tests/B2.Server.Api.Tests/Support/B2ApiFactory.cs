using B2.Server.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace B2.Server.Api.Tests.Support;

/// <summary>Integration tests: isolated SQLite DB under a temp <c>B2:DataDirectory</c>.</summary>
public sealed class B2ApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "b2-api-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dataDirectory);
        builder.UseEnvironment("Testing");
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
