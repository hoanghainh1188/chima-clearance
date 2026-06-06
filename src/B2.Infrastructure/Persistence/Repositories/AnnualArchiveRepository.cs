using System.Globalization;
using B2.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

/// <summary>
/// FR-021 / INV-12 — reads and bulk-deletes archivable data from the live database.
/// Deletion order: AuditLogs → Vehicles → Declarations → DailyHeaders
/// (DeleteBehavior.Restrict means we cannot rely on EF Core cascade).
/// </summary>
public sealed class AnnualArchiveRepository : IAnnualArchiveRepository
{
    private readonly B2DbContext _db;

    public AnnualArchiveRepository(B2DbContext db) => _db = db;

    public async Task<IReadOnlyList<int>> GetYearsOlderThanAsync(DateOnly cutoff, CancellationToken ct)
    {
        var years = await _db.Declarations
            .Where(d => d.BusinessDate < cutoff)
            .Select(d => d.BusinessDate.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToListAsync(ct);
        return years;
    }

    public async Task<int> CountDeclarationsAsync(DateOnly from, DateOnly to, CancellationToken ct) =>
        await _db.Declarations
            .CountAsync(d => d.BusinessDate >= from && d.BusinessDate <= to, ct);

    public async Task DeleteAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // Collect IDs in application memory (SQLite has limited subquery support for bulk ops).
        var declarationIds = await _db.Declarations
            .Where(d => d.BusinessDate >= from && d.BusinessDate <= to)
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (declarationIds.Count == 0)
            return;

        var vehicleIds = await _db.Vehicles
            .Where(v => declarationIds.Contains(v.DeclarationId))
            .Select(v => v.Id)
            .ToListAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // 1. AuditLog rows for declarations and vehicles
        await _db.AuditLogs
            .Where(a => (a.EntityType == "Declaration" && declarationIds.Contains(a.EntityId))
                     || (a.EntityType == "Vehicle"      && vehicleIds.Contains(a.EntityId)))
            .ExecuteDeleteAsync(ct);

        // 2. Vehicles
        await _db.Vehicles
            .Where(v => declarationIds.Contains(v.DeclarationId))
            .ExecuteDeleteAsync(ct);

        // 3. Declarations
        await _db.Declarations
            .Where(d => declarationIds.Contains(d.Id))
            .ExecuteDeleteAsync(ct);

        // 4. DailyHeaders whose date falls in range and no declarations remain
        await _db.DailyHeaders
            .Where(h => h.Date >= from && h.Date <= to)
            .ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }
}
