using Microsoft.Extensions.Configuration;

namespace B2.Infrastructure.Backup;

/// <summary>One Litestream file-replica target (e.g. a USB or LAN backup folder).</summary>
public sealed record LitestreamReplica(string Name, string Path);

/// <summary>
/// F-15 / T-S5-08 continuous-backup config (bound from <c>B2:Litestream</c>). On Windows it is
/// ON by default — the installer copies <c>litestream.exe</c> into the data directory and ships
/// no config, and the site has no IT staff to write one — replicating to
/// <c>{data}\backup\replica</c>. Configuration can disable it or add USB/LAN replicas.
/// </summary>
public sealed class LitestreamOptions
{
    public const string DefaultReplicaName = "local";
    public bool Enabled { get; set; }

    public string ExecutablePath { get; set; } = string.Empty;

    public string DbPath { get; set; } = string.Empty;

    public IList<LitestreamReplica> Replicas { get; } = new List<LitestreamReplica>();

    /// <summary>
    /// Builds the options from <c>B2:Litestream</c>, filling production defaults for every key
    /// that is absent: enabled on Windows, <c>{data}\litestream.exe</c>, <c>{data}\db\b2.db</c>,
    /// and a single local replica <c>{data}\backup\replica</c> when none is configured.
    /// </summary>
    public static LitestreamOptions FromConfiguration(IConfigurationSection section, string dataDirectory, bool isWindows)
    {
        ArgumentNullException.ThrowIfNull(section);

        var enabledSetting = section["Enabled"];
        var executable = section["ExecutablePath"];
        var dbPath = section["DbPath"];
        var options = new LitestreamOptions
        {
            Enabled = string.IsNullOrWhiteSpace(enabledSetting)
                ? isWindows
                : bool.TryParse(enabledSetting, out var enabled) && enabled,
            ExecutablePath = string.IsNullOrWhiteSpace(executable)
                ? Path.Combine(dataDirectory, "litestream.exe")
                : executable,
            DbPath = string.IsNullOrWhiteSpace(dbPath) ? Path.Combine(dataDirectory, "db", "b2.db") : dbPath,
        };

        foreach (var child in section.GetSection("Replicas").GetChildren())
        {
            var path = child["Path"];
            if (!string.IsNullOrWhiteSpace(path))
            {
                options.Replicas.Add(new LitestreamReplica(child["Name"] ?? string.Empty, path));
            }
        }

        if (options.Replicas.Count == 0)
        {
            options.Replicas.Add(new LitestreamReplica(
                DefaultReplicaName, Path.Combine(dataDirectory, "backup", "replica")));
        }

        return options;
    }
}
