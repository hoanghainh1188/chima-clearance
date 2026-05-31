namespace B2.Shared.Contracts.Admin;

/// <summary>F-18 health dashboard payload (wireframe § 6.11). Raw metrics + the two
/// thresholded warning flags; the desktop renders the localized labels + status icons.</summary>
public sealed record HealthReportDto(
    long UptimeSeconds,
    long WorkingSetBytes,
    long DbFileBytes,
    long FreeDiskBytes,
    int OpenDeclarationsOlderThan24h,
    bool DiskSpaceWarning,
    bool OpenDeclarationsWarning);
