using B2.Application.Abstractions;
using B2.Application.Export;
using B2.Domain.Declarations;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class SoB2ExportRepository : ISoB2ExportRepository
{
    private readonly B2DbContext _db;

    public SoB2ExportRepository(B2DbContext db) => _db = db;

    public async Task<IReadOnlyList<SoB2DeclarationBlock>> GetDeclarationsForExportAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken)
    {
        // Two flat queries stitched in memory: SQLite cannot ORDER BY DateTimeOffset, and a
        // nested collection projection across a left join is brittle to translate. Cancelled
        // declarations are intentionally kept (FR-023 — strike-through, not omit).
        var declarations = await (
                from d in _db.Declarations.AsNoTracking()
                where d.BusinessDate >= fromDate && d.BusinessDate <= toDate
                join p in _db.Processors.AsNoTracking() on d.ProcessorId equals p.Id
                join y in _db.Yards.AsNoTracking() on d.YardId equals y.Id
                join eu in _db.Users.AsNoTracking() on d.EnteredByUserId equals eu.Id
                join ku0 in _db.Users.AsNoTracking() on d.KvgsConfirmedByUserId equals ku0.Id into kuj
                from ku in kuj.DefaultIfEmpty()
                select new DeclarationRow(
                    d.Id,
                    d.BusinessDate,
                    d.DeclarationNumber,
                    p.FullName,
                    d.CompanyName,
                    d.GoodsName,
                    y.Name,
                    d.Status,
                    eu.FullName,
                    ku != null ? ku.FullName : null,
                    d.EnteredAt))
            .ToListAsync(cancellationToken);

        if (declarations.Count == 0)
        {
            return Array.Empty<SoB2DeclarationBlock>();
        }

        var declarationIds = declarations.Select(d => d.Id).ToList();
        var numbersByDeclaration = (await _db.DeclarationNumbers.AsNoTracking()
                .Where(n => declarationIds.Contains(n.DeclarationId))
                .OrderBy(n => n.Ordinal)
                .Select(n => new { n.DeclarationId, n.Number })
                .ToListAsync(cancellationToken))
            .GroupBy(n => n.DeclarationId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(n => n.Number).ToList());
        var vehicles = await _db.Vehicles.AsNoTracking()
            .Where(v => declarationIds.Contains(v.DeclarationId))
            .Select(v => new VehicleRow(
                v.DeclarationId,
                v.PlateNumber,
                v.TrailerOrContainerNumber,
                v.EntryTime,
                v.ExitMarked,
                v.ExitTime,
                v.Note))
            .ToListAsync(cancellationToken);

        var vehiclesByDeclaration = vehicles
            .GroupBy(v => v.DeclarationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SoB2VehicleRow>)g
                    .OrderBy(v => v.EntryTime)
                    .Select(v => new SoB2VehicleRow(v.PlateNumber, v.TrailerOrContainerNumber, v.EntryTime, v.ExitMarked, v.ExitTime, v.Note))
                    .ToList());

        return declarations
            .OrderBy(d => d.BusinessDate)
            .ThenBy(d => d.EnteredAt)
            .Select(d => new SoB2DeclarationBlock(
                d.BusinessDate,
                d.DeclarationNumber,
                numbersByDeclaration.GetValueOrDefault(d.Id, new[] { d.DeclarationNumber }),
                d.ProcessorName,
                d.CompanyName,
                d.GoodsName,
                d.YardName,
                KvgsConfirmed: d.Status == DeclarationStatus.KVGSConfirmed,
                IsAllExited: d.Status == DeclarationStatus.AllExited,
                IsCancelled: d.Status == DeclarationStatus.Cancelled,
                d.EnteredByName,
                d.KvgsConfirmedByName,
                vehiclesByDeclaration.GetValueOrDefault(d.Id, Array.Empty<SoB2VehicleRow>())))
            .ToList();
    }

    private sealed record DeclarationRow(
        Guid Id,
        DateOnly BusinessDate,
        string DeclarationNumber,
        string ProcessorName,
        string CompanyName,
        string GoodsName,
        string YardName,
        DeclarationStatus Status,
        string EnteredByName,
        string? KvgsConfirmedByName,
        DateTimeOffset EnteredAt);

    private sealed record VehicleRow(
        Guid DeclarationId,
        string PlateNumber,
        string? TrailerOrContainerNumber,
        DateTimeOffset EntryTime,
        bool ExitMarked,
        DateTimeOffset? ExitTime,
        string? Note);
}
