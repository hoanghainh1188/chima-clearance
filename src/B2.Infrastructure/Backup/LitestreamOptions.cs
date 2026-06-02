namespace B2.Infrastructure.Backup;

/// <summary>One Litestream file-replica target (e.g. a USB or LAN backup folder).</summary>
public sealed record LitestreamReplica(string Name, string Path);

/// <summary>
/// F-15 / T-S5-08 continuous-backup config (bound from <c>B2:Litestream</c>). Disabled by
/// default; the Windows installer ships the litestream binary + the real config.
/// </summary>
public sealed class LitestreamOptions
{
    public bool Enabled { get; set; }

    public string ExecutablePath { get; set; } = string.Empty;

    public string DbPath { get; set; } = string.Empty;

    public IList<LitestreamReplica> Replicas { get; } = new List<LitestreamReplica>();
}
