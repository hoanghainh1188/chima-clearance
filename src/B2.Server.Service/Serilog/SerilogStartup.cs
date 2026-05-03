using System.Globalization;
using Serilog;
using Serilog.Events;

namespace B2.Server.Service.Serilog;

public static class SerilogStartup
{
    /// <summary>Rolling file under <c>{DataDirectory}/logs/b2-yyyymmdd.log</c> plus in-memory sink.</summary>
    public static void Configure(LoggerConfiguration cfg, IConfiguration configuration, InMemoryLogStore memorySink)
    {
        var dataDir = configuration["B2:DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            var root = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dataDir = Path.Combine(root, "B2");
        }

        var logDir = Path.Combine(dataDir, "logs");
        Directory.CreateDirectory(logDir);
        var rollingPath = Path.Combine(logDir, "b2-.log");
        var invariant = CultureInfo.InvariantCulture;

        cfg
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: invariant)
            .WriteTo.File(
                rollingPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                formatProvider: invariant)
            .WriteTo.Sink(memorySink);
    }
}
