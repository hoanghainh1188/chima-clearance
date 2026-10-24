using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Domain.Declarations;
using B2.Shared.Contracts.Reports;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class ReportsRepository : IReportsRepository
{
    private readonly B2DbContext _db;

    public ReportsRepository(B2DbContext db) => _db = db;

    public async Task<IReadOnlyList<ProcessorReportItemDto>> GetByProcessorAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        var q = BaseVehicleQuery(fromDate, toDate, includeCancelled);
        return await (
                from v in q
                join p in _db.Processors.AsNoTracking() on v.Declaration.ProcessorId equals p.Id
                group v by new { p.Id, p.FullName }
                into g
                select new ProcessorReportItemDto(g.Key.Id, g.Key.FullName, g.Count()))
            .OrderByDescending(x => x.TotalVehicles)
            .ThenBy(x => x.ProcessorName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<YardReportItemDto>> GetByYardAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        var q = BaseVehicleQuery(fromDate, toDate, includeCancelled);
        return await (
                from v in q
                join y in _db.Yards.AsNoTracking() on v.Declaration.YardId equals y.Id
                group v by new { y.Id, y.Code, y.Name }
                into g
                select new YardReportItemDto(g.Key.Id, g.Key.Code, g.Key.Name, g.Count()))
            .OrderByDescending(x => x.TotalVehicles)
            .ThenBy(x => x.YardCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HourReportItemDto>> GetByHourAsync(
        DateOnly date,
        int bucketMinutes,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        // #154: bucket the vehicles that physically exited on `date` (Vietnam time), whatever
        // day their declaration was opened. julianday() normalises the stored offset to UTC
        // because EF cannot translate DateTimeOffset comparisons on SQLite.
        var (startUtc, endUtc) = VietnamTime.BusinessDayUtcBounds(date);
        var includeCancelledFlag = includeCancelled ? 1 : 0;
        var rows = await _db.Database
            .SqlQuery<DateTimeOffset>(
                $"""
                 SELECT v."ExitTime" AS "Value"
                 FROM "Vehicles" AS v
                 INNER JOIN "Declarations" AS d ON d."Id" = v."DeclarationId"
                 WHERE v."ExitMarked" = 1
                   AND v."ExitTime" IS NOT NULL
                   AND ({includeCancelledFlag} = 1 OR d."Status" <> 'Cancelled')
                   AND julianday(v."ExitTime") >= julianday({startUtc})
                   AND julianday(v."ExitTime") < julianday({endUtc})
                 """)
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(t => BucketLabel(t, bucketMinutes))
            .Select(g => new HourReportItemDto(g.Key, g.Count()))
            .OrderBy(x => x.BucketLabel)
            .ToList();
    }

    public async Task<IReadOnlyList<OfficerReportItemDto>> GetByOfficerAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        // Snapshot transaction: the entry-count and KVGS-count reads must observe
        // the same DB state. Without it a concurrent WAL writer can land between
        // the two ToListAsync calls and skew one tally against the other.
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var entered = await _db.Declarations.AsNoTracking()
            .Where(d => d.BusinessDate >= fromDate && d.BusinessDate <= toDate)
            .Where(d => includeCancelled || d.Status != DeclarationStatus.Cancelled)
            .Join(
                _db.Users.AsNoTracking(),
                d => d.EnteredByUserId,
                u => u.Id,
                (d, u) => new { Declaration = d, User = u })
            .GroupBy(x => new { x.User.Id, x.User.FullName })
            .Select(g => new { OfficerUserId = g.Key.Id, g.Key.FullName, VehiclesEntered = g.Sum(x => x.Declaration.Vehicles.Count) })
            .ToListAsync(cancellationToken);

        var kvgs = await _db.Declarations.AsNoTracking()
            .Where(d => d.BusinessDate >= fromDate && d.BusinessDate <= toDate)
            .Where(d => includeCancelled || d.Status != DeclarationStatus.Cancelled)
            .Where(d => d.KvgsConfirmedByUserId != null)
            .Join(
                _db.Users.AsNoTracking(),
                d => d.KvgsConfirmedByUserId!.Value,
                u => u.Id,
                (d, u) => new { d.KvgsConfirmedByUserId, u.FullName })
            .GroupBy(x => new { x.KvgsConfirmedByUserId, x.FullName })
            .Select(g => new { OfficerUserId = g.Key.KvgsConfirmedByUserId!.Value, g.Key.FullName, Count = g.Count() })
            .ToListAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        // Union both populations so an officer who only KVGS-confirmed (never entered
        // a declaration) still appears — previously dropped because the projection
        // seeded exclusively from `entered`.
        var byOfficer = new Dictionary<Guid, (string Name, int Entered, int Kvgs)>();
        foreach (var e in entered)
        {
            byOfficer[e.OfficerUserId] = (e.FullName, e.VehiclesEntered, 0);
        }

        foreach (var k in kvgs)
        {
            byOfficer[k.OfficerUserId] = byOfficer.TryGetValue(k.OfficerUserId, out var cur)
                ? (cur.Name, cur.Entered, k.Count)
                : (k.FullName, 0, k.Count);
        }

        return byOfficer
            .Select(x => new OfficerReportItemDto(x.Key, x.Value.Name, x.Value.Entered, x.Value.Kvgs))
            .OrderByDescending(x => x.VehiclesEntered)
            .ThenBy(x => x.OfficerName)
            .ToList();
    }

    public async Task<IReadOnlyList<PendingReportItemDto>> GetPendingAsync(
        DateTimeOffset asOfUtc,
        bool includeCancelled,
        CancellationToken cancellationToken)
    {
        var q = _db.Vehicles.AsNoTracking()
            .Where(v => !v.ExitMarked)
            .Where(v => includeCancelled || v.Declaration.Status != DeclarationStatus.Cancelled);

        var rows = await q
            .Select(v => new { v.DeclarationId, v.Id, v.Declaration.DeclarationNumber, v.PlateNumber, v.EntryTime })
            .ToListAsync(cancellationToken);
        return rows
            .Select(v => new PendingReportItemDto(
                v.DeclarationId,
                v.Id,
                v.DeclarationNumber,
                v.PlateNumber,
                v.EntryTime,
                Math.Round((asOfUtc - v.EntryTime).TotalHours, 2)))
            .OrderByDescending(x => x.HoursInside)
            .ToList();
    }

    private IQueryable<Domain.Declarations.Vehicle> BaseVehicleQuery(DateOnly fromDate, DateOnly toDate, bool includeCancelled)
    {
        // No Include: every caller only touches v.Declaration.* inside a server-side
        // projection/predicate, which EF translates to a SQL join. Materialising the
        // Declaration navigation would only bloat the result set.
        var q = _db.Vehicles.AsNoTracking()
            .Where(v => v.Declaration.BusinessDate >= fromDate && v.Declaration.BusinessDate <= toDate);
        if (!includeCancelled)
        {
            q = q.Where(v => v.Declaration.Status != DeclarationStatus.Cancelled);
        }

        return q;
    }

    private static string BucketLabel(DateTimeOffset timestamp, int bucketMinutes)
    {
        var local = TimeZoneInfo.ConvertTime(timestamp, VietnamTime.TimeZone);
        var startMinute = (local.Minute / bucketMinutes) * bucketMinutes;
        var start = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, startMinute, 0, local.Offset);
        var end = start.AddMinutes(bucketMinutes);
        return $"{start:HH:mm}-{end:HH:mm}";
    }
}
