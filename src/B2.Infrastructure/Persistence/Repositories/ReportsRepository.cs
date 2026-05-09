using System.Data;
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
        // Snapshot both aggregates under one isolation level so counts cannot drift mid-report.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var entered = await _db.Declarations
            .Where(d => d.BusinessDate >= fromDate && d.BusinessDate <= toDate)
            .Where(d => includeCancelled || d.Status != DeclarationStatus.Cancelled)
            .Join(
                _db.Users.AsNoTracking(),
                d => d.EnteredByUserId,
                u => u.Id,
                (d, u) => new { Declaration = d, User = u })
            .GroupBy(x => new { x.User.Id, x.User.FullName })
            .Select(g => new { EnteredByUserId = g.Key.Id, g.Key.FullName, VehiclesEntered = g.Sum(x => x.Declaration.Vehicles.Count) })
            .ToListAsync(cancellationToken);

        var kvgs = await _db.Declarations
            .Where(d => d.BusinessDate >= fromDate && d.BusinessDate <= toDate)
            .Where(d => includeCancelled || d.Status != DeclarationStatus.Cancelled)
            .Where(d => d.KvgsConfirmedByUserId != null)
            .GroupBy(d => d.KvgsConfirmedByUserId!.Value)
            .Select(g => new { OfficerUserId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var vehiclesEnteredByOfficer = entered.ToDictionary(x => x.EnteredByUserId, x => x.VehiclesEntered);
        var namesByOfficer = entered.ToDictionary(x => x.EnteredByUserId, x => x.FullName);
        var kvgsByOfficer = kvgs.ToDictionary(x => x.OfficerUserId, x => x.Count);

        var officerIds = vehiclesEnteredByOfficer.Keys.Union(kvgsByOfficer.Keys).ToArray();
        var missingNameIds = officerIds.Where(id => !namesByOfficer.ContainsKey(id)).ToArray();
        if (missingNameIds.Length > 0)
        {
            var extraNames = await _db.Users.AsNoTracking()
                .Where(u => missingNameIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToListAsync(cancellationToken);
            foreach (var row in extraNames)
            {
                namesByOfficer[row.Id] = row.FullName;
            }
        }

        await tx.CommitAsync(cancellationToken);

        return officerIds
            .Select(id => new OfficerReportItemDto(
                id,
                namesByOfficer.GetValueOrDefault(id, string.Empty),
                vehiclesEnteredByOfficer.GetValueOrDefault(id),
                kvgsByOfficer.GetValueOrDefault(id)))
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
