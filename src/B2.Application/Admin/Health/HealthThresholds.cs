namespace B2.Application.Admin.Health;

/// <summary>F-18 health-check thresholds (SC / wireframe § 6.11).</summary>
public static class HealthThresholds
{
    /// <summary>Disk-space warning fires below 1 GB free (spec.md F-18 scenario).</summary>
    public const long LowDiskBytes = 1L * 1024 * 1024 * 1024;

    public static bool IsLowDisk(long freeBytes) => freeBytes < LowDiskBytes;
}
