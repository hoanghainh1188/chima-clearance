using System.Diagnostics;
using B2.Application.Abstractions;
using B2.Application.Admin.Health;
using B2.Domain.Declarations;
using B2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Diagnostics;

/// <summary>F-18 — gathers process (uptime, working set), storage (DB file size, free disk),
/// and data (open declarations older than 24 h) health metrics.</summary>
public sealed class SystemHealthProbe : ISystemHealthProbe
{
    private readonly B2DbContext _db;
    private readonly IClock _clock;
    private readonly string _dbPath;

    public SystemHealthProbe(B2DbContext db, IClock clock, string dbPath)
    {
        _db = db;
        _clock = clock;
        _dbPath = dbPath;
    }

    public async Task<SystemHealthSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();
        var uptimeSeconds = (long)(_clock.UtcNow - new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero)).TotalSeconds;

        var dbBytes = File.Exists(_dbPath) ? new FileInfo(_dbPath).Length : 0L;
        var freeDiskBytes = FreeDiskBytes(_dbPath);

        var threshold = _clock.UtcNow.AddHours(-24);
        // SQLite can't compare DateTimeOffset in SQL reliably; project the (few) open
        // declarations' entry times and count the stale ones in memory.
        var openEntryTimes = await _db.Declarations.AsNoTracking()
            .Where(d => d.Status == DeclarationStatus.Open)
            .Select(d => d.EnteredAt)
            .ToListAsync(cancellationToken);
        var openOld = openEntryTimes.Count(t => t < threshold);

        return new SystemHealthSnapshot(
            Math.Max(0, uptimeSeconds),
            process.WorkingSet64,
            dbBytes,
            freeDiskBytes,
            openOld);
    }

    private static long FreeDiskBytes(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            return string.IsNullOrEmpty(root) ? 0L : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return 0L;
        }
    }
}
