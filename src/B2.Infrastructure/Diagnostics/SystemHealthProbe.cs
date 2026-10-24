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

        // #177: in WAL mode uncheckpointed pages live in -wal (often hundreds of MB), so the
        // on-disk footprint is the sum of all three files.
        var dbBytes = FileLength(_dbPath) + FileLength(_dbPath + "-wal") + FileLength(_dbPath + "-shm");
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

    private static long FileLength(string path) => File.Exists(path) ? new FileInfo(path).Length : 0L;
}
