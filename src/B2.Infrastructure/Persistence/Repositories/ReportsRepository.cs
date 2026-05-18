using B2.Application.Abstractions;
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
        var q = BaseVehicleQuery(date, date, includeCancelled).Where(v => v.ExitMarked && v.ExitTime != null);
        var rows = await q.Select(v => v.ExitTime!.Value).ToListAsync(cancellationToken);
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
        var dateFiltered = _db.Declarations
            .Where(d => d.BusinessDate >= fromDate && d.BusinessDate <= toDate)
            .Where(d => includeCancelled || d.Status != DeclarationStatus.Cancelled);

        var enteredRows = await dateFiltered
            .GroupBy(d => d.EnteredByUserId)
            .Select(g => new { UserId = g.Key, VehiclesEntered = g.Sum(x => x.Vehicles.Count) })
            .ToListAsync(cancellationToken);

        var kvgsRows = await dateFiltered
            .Where(d => d.KvgsConfirmedByUserId != null)
            .GroupBy(d => d.KvgsConfirmedByUserId!.Value)
            .Select(g => new { UserId = g.Key, KvgsCount = g.Count() })
            .ToListAsync(cancellationToken);

        var enteredMap = enteredRows.ToDictionary(x => x.UserId, x => x.VehiclesEntered);
        var kvgsMap = kvgsRows.ToDictionary(x => x.UserId, x => x.KvgsCount);
        var allOfficerIds = enteredMap.Keys.Union(kvgsMap.Keys).ToList();
        if (allOfficerIds.Count == 0)
        {
            return [];
        }

        var names = await _db.Users.AsNoTracking()
            .Where(u => allOfficerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        return allOfficerIds
            .Select(id => new OfficerReportItemDto(
                id,
                names.GetValueOrDefault(id) ?? string.Empty,
                enteredMap.GetValueOrDefault(id),
                kvgsMap.GetValueOrDefault(id)))
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
        var q = _db.Vehicles.AsNoTracking()
            .Where(v => v.Declaration.BusinessDate >= fromDate && v.Declaration.BusinessDate <= toDate);
        if (!includeCancelled)
        {
            q = q.Where(v => v.Declaration.Status != DeclarationStatus.Cancelled);
        }

        return q.Include(v => v.Declaration);
    }

    private static string BucketLabel(DateTimeOffset timestamp, int bucketMinutes)
    {
        var tz = ResolveVietnamTimeZone();
        var local = TimeZoneInfo.ConvertTime(timestamp, tz);
        var startMinute = (local.Minute / bucketMinutes) * bucketMinutes;
        var start = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, startMinute, 0, local.Offset);
        var end = start.AddMinutes(bucketMinutes);
        return $"{start:HH:mm}-{end:HH:mm}";
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
