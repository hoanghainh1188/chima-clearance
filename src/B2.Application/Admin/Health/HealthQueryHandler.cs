using B2.Application.Abstractions;
using B2.Shared.Contracts.Admin;

namespace B2.Application.Admin.Health;

/// <summary>F-18 — assembles the health dashboard: reads the raw probe metrics and applies
/// the warning thresholds (Admin only).</summary>
public sealed class HealthQueryHandler
{
    private readonly ISystemHealthProbe _probe;

    public HealthQueryHandler(ISystemHealthProbe probe) => _probe = probe;

    public async Task<HealthReportDto> HandleAsync(CancellationToken cancellationToken)
    {
        var s = await _probe.ReadAsync(cancellationToken);
        return new HealthReportDto(
            s.UptimeSeconds,
            s.WorkingSetBytes,
            s.DbFileBytes,
            s.FreeDiskBytes,
            s.OpenDeclarationsOlderThan24h,
            DiskSpaceWarning: HealthThresholds.IsLowDisk(s.FreeDiskBytes),
            OpenDeclarationsWarning: s.OpenDeclarationsOlderThan24h > 0);
    }
}
