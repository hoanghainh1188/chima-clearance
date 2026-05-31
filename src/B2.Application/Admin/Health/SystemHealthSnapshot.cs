namespace B2.Application.Admin.Health;

/// <summary>Raw, un-thresholded health metrics gathered by the infrastructure probe (F-18).</summary>
public sealed record SystemHealthSnapshot(
    long UptimeSeconds,
    long WorkingSetBytes,
    long DbFileBytes,
    long FreeDiskBytes,
    int OpenDeclarationsOlderThan24h);
