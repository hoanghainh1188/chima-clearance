using System.Data;
using B2.Application.Abstractions;
using B2.Application.Declarations.Exit;
using B2.Application.Declarations.List;
using B2.Domain;
using B2.Domain.Declarations;
using B2.Domain.Users;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class DeclarationRepository : IDeclarationRepository
{
    private readonly B2DbContext _db;

    public DeclarationRepository(B2DbContext db)
    {
        _db = db;
    }

    public Task<bool> DeclarationNumberExistsAsync(string declarationNumber, CancellationToken cancellationToken) =>
        _db.Declarations.AnyAsync(d => d.DeclarationNumber == declarationNumber, cancellationToken);

    public async Task AddAsync(Declaration declaration, CancellationToken cancellationToken) =>
        await _db.Declarations.AddAsync(declaration, cancellationToken);

    public Task<Declaration?> GetByIdWithVehiclesAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Declarations.Include(d => d.Vehicles).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Declaration?> GetByDeclarationNumberWithVehiclesAsync(
        string declarationNumber,
        CancellationToken cancellationToken) =>
        _db.Declarations.Include(d => d.Vehicles)
            .FirstOrDefaultAsync(d => d.DeclarationNumber == declarationNumber, cancellationToken);

    public async Task<MarkVehicleExitResult> MarkVehicleExitAsync(
        Guid vehicleId,
        Guid actorUserId,
        DateTimeOffset exitTime,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var vehicle = await _db.Vehicles
                .Include(v => v.Declaration)
                .ThenInclude(d => d!.Vehicles)
                .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

            if (vehicle?.Declaration is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new MarkVehicleExitNotFound();
            }

            if (vehicle.ExitMarked)
            {
                var winnerName = await WinnerNameAsync(vehicle.ExitConfirmedByUserId, cancellationToken);
                await tx.RollbackAsync(cancellationToken);
                return new MarkVehicleExitConflict(winnerName, vehicle.ExitTime ?? exitTime);
            }

            vehicle.MarkExit(actorUserId, exitTime);
            vehicle.Declaration.RecomputeStatus();
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new MarkVehicleExitApplied(
                vehicle.Id,
                vehicle.ExitTime!.Value,
                vehicle.Declaration.Id,
                vehicle.Declaration.Status.ToString());
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Declaration> CancelDeclarationAsync(
        Guid declarationId,
        Role actorRole,
        Guid actorUserId,
        string reason,
        DateTimeOffset cancelledAtUtc,
        CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var declaration = await _db.Declarations
                .Include(d => d.Vehicles)
                .FirstOrDefaultAsync(d => d.Id == declarationId, cancellationToken);

            if (declaration is null)
            {
                await tx.RollbackAsync(cancellationToken);
                throw new DomainException("DECLARATION_NOT_FOUND", "Declaration not found.", 404);
            }

            declaration.Cancel(actorRole, actorUserId, reason, cancelledAtUtc);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return declaration;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<UnExitVehicleResult> UnExitVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        // FR-022 / INV-9: read + mutate under Serializable so a concurrent cancel (also
        // Serializable) cannot be silently overwritten by a stale in-memory Open graph.
        // Mirrors CancelDeclarationAsync / the exit path fixed in #60.
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var vehicle = await _db.Vehicles
                .Include(v => v.Declaration)
                .ThenInclude(d => d!.Vehicles)
                .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);

            if (vehicle?.Declaration is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new UnExitVehicleNotFound();
            }

            if (!vehicle.ExitMarked)
            {
                await tx.RollbackAsync(cancellationToken);
                return new UnExitVehicleInvalidState("Vehicle is not marked as exited.");
            }

            vehicle.UnExit();
            vehicle.Declaration.RecomputeStatus();
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new UnExitVehicleApplied(vehicle.Id, vehicle.Declaration.Id, vehicle.Declaration.Status.ToString());
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<string> WinnerNameAsync(Guid? exitConfirmedByUserId, CancellationToken cancellationToken)
    {
        if (exitConfirmedByUserId is null)
        {
            return "?";
        }

        var name = await _db.Users.AsNoTracking()
            .Where(u => u.Id == exitConfirmedByUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? "?" : name;
    }

    public async Task<(IReadOnlyList<DeclarationSummaryRow> Items, int TotalCount)> ListDeclarationsAsync(
        ListDeclarationsRepositoryQuery query,
        CancellationToken cancellationToken)
    {
        var baseQuery = _db.Declarations.AsNoTracking()
            .Where(d => d.BusinessDate >= query.FromDate && d.BusinessDate <= query.ToDate);

        var excludeCancelled = !query.IncludeCancelled
            && (string.IsNullOrWhiteSpace(query.StatusFilter)
                || query.StatusFilter.Equals("Default", StringComparison.OrdinalIgnoreCase));
        if (excludeCancelled)
        {
            baseQuery = baseQuery.Where(d => d.Status != DeclarationStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(query.StatusFilter)
            && !query.StatusFilter.Equals("All", StringComparison.OrdinalIgnoreCase)
            && !query.StatusFilter.Equals("Default", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse<DeclarationStatus>(query.StatusFilter, ignoreCase: true, out var parsedStatus))
        {
            baseQuery = baseQuery.Where(d => d.Status == parsedStatus);
        }

        if (query.ProcessorIds is { Count: > 0 } pids)
        {
            baseQuery = baseQuery.Where(d => pids.Contains(d.ProcessorId));
        }

        if (query.YardIds is { Count: > 0 } yids)
        {
            baseQuery = baseQuery.Where(d => yids.Contains(d.YardId));
        }

        if (query.OfficerUserId is { } officerId)
        {
            baseQuery = baseQuery.Where(d =>
                d.EnteredByUserId == officerId || d.KvgsConfirmedByUserId == officerId);
        }

        if (!string.IsNullOrWhiteSpace(query.PlateFilter))
        {
            var plate = query.PlateFilter.Trim();
            var declIdsMatchingPlate = _db.Vehicles.AsNoTracking()
                .Where(v => v.PlateNumber.Contains(plate))
                .Select(v => v.DeclarationId);
            baseQuery = baseQuery.Where(d =>
                d.DeclarationNumber.Contains(plate) || declIdsMatchingPlate.Contains(d.Id));
        }

        if (query.SinceUtc is { } since)
        {
            // SQLite EF cannot translate DateTimeOffset predicates; apply "since" filter in-memory then SQL IN.
            var declarationTimeRows = await baseQuery
                .Select(d => new { d.Id, d.EnteredAt, d.KvgsConfirmedAt })
                .ToListAsync(cancellationToken);
            if (declarationTimeRows.Count > 0)
            {
                var declIds = declarationTimeRows.Select(x => x.Id).ToList();
                var vehicleRows = await _db.Vehicles.AsNoTracking()
                    .Where(v => declIds.Contains(v.DeclarationId))
                    .Select(v => new { v.DeclarationId, v.EntryTime, v.ExitTime })
                    .ToListAsync(cancellationToken);
                var byDecl = vehicleRows.GroupBy(v => v.DeclarationId)
                    .ToDictionary(g => g.Key, g => g.ToList());
                var matching = new HashSet<Guid>();
                foreach (var row in declarationTimeRows)
                {
                    if (row.EnteredAt > since || (row.KvgsConfirmedAt is not null && row.KvgsConfirmedAt > since))
                    {
                        matching.Add(row.Id);
                        continue;
                    }

                    if (!byDecl.TryGetValue(row.Id, out var vs))
                    {
                        continue;
                    }

                    if (vs.Any(v => v.EntryTime > since || (v.ExitTime is not null && v.ExitTime > since)))
                    {
                        matching.Add(row.Id);
                    }
                }

                if (matching.Count == 0)
                {
                    baseQuery = baseQuery.Where(_ => false);
                }
                else
                {
                    var idSet = matching.ToList();
                    baseQuery = baseQuery.Where(d => idSet.Contains(d.Id));
                }
            }
        }

        // SQLite provider cannot translate ORDER BY on DateTimeOffset. For typical daily volume we
        // project (Id, EnteredAt) for the filtered set, sort in-memory, then page (F-10 ledger).
        var total = await baseQuery.CountAsync(cancellationToken);
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 50 : query.PageSize;
        var idAndEnteredAt = await baseQuery
            .Select(d => new { d.Id, d.EnteredAt })
            .ToListAsync(cancellationToken);
        var sortedSlice = idAndEnteredAt
            .OrderByDescending(x => x.EnteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        if (sortedSlice.Count == 0)
        {
            return (Array.Empty<DeclarationSummaryRow>(), total);
        }

        var ids = sortedSlice.Select(s => s.Id).ToList();
        var rows = await (
                from d in _db.Declarations.AsNoTracking()
                join u in _db.Users.AsNoTracking() on d.EnteredByUserId equals u.Id
                join p in _db.Processors.AsNoTracking() on d.ProcessorId equals p.Id
                join y in _db.Yards.AsNoTracking() on d.YardId equals y.Id
                where ids.Contains(d.Id)
                select new DeclarationSummaryRow(
                    d.Id,
                    d.DeclarationNumber,
                    d.CompanyName,
                    d.GoodsName,
                    d.Status.ToString(),
                    d.Vehicles.Count(),
                    d.Vehicles.Count(v => !v.ExitMarked),
                    d.ProcessorId,
                    p.FullName,
                    d.YardId,
                    y.Code,
                    y.Name,
                    d.EnteredByUserId,
                    u.FullName,
                    d.EnteredAt,
                    d.BusinessDate,
                    d.KvgsConfirmedByUserId,
                    d.KvgsConfirmedAt,
                    string.Empty,
                    null))
            .ToListAsync(cancellationToken);

        var orderIndex = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        rows.Sort((a, b) => orderIndex[a.Id].CompareTo(orderIndex[b.Id]));

        var firstVehicles = await _db.Vehicles.AsNoTracking()
            .Where(v => ids.Contains(v.DeclarationId))
            .Select(v => new { v.DeclarationId, v.EntryTime, v.PlateNumber, v.TrailerOrContainerNumber })
            .ToListAsync(cancellationToken);
        var firstByDecl = firstVehicles
            .GroupBy(v => v.DeclarationId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.EntryTime).First());

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            string plate = string.Empty;
            string? trailer = null;
            if (firstByDecl.TryGetValue(r.Id, out var fv))
            {
                plate = fv.PlateNumber;
                trailer = fv.TrailerOrContainerNumber;
            }

            rows[i] = r with { PrimaryPlateNumber = plate, PrimaryTrailerOrContainer = trailer };
        }

        return (rows, total);
    }

    public async Task<DeclarationDetailResponse?> GetDeclarationDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await (
                from dec in _db.Declarations.AsNoTracking()
                join p in _db.Processors.AsNoTracking() on dec.ProcessorId equals p.Id
                join y in _db.Yards.AsNoTracking() on dec.YardId equals y.Id
                where dec.Id == id
                select new { dec, Processor = p, Yard = y })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        // SQLite cannot ORDER BY a DateTimeOffset column, so materialize first
        // and sort the entry times in memory (LINQ to Objects).
        var vehicles = (await _db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == id)
                .Select(v => new VehicleDetailDto(
                    v.Id,
                    v.PlateNumber,
                    v.TrailerOrContainerNumber,
                    v.EntryTime,
                    v.ExitMarked,
                    v.ExitTime))
                .ToListAsync(cancellationToken))
            .OrderBy(v => v.EntryTime)
            .ToList();

        var d = row.dec;
        var dto = new DeclarationDto(
            d.Id,
            d.DeclarationNumber,
            new ProcessorDto(row.Processor.Id, row.Processor.FullName, row.Processor.IsActive),
            d.CompanyName,
            d.GoodsName,
            new YardDto(row.Yard.Id, row.Yard.Code, row.Yard.Name, row.Yard.IsActive),
            d.Status.ToString(),
            d.EnteredByUserId,
            d.EnteredAt,
            d.BusinessDate,
            d.KvgsConfirmedByUserId,
            d.KvgsConfirmedAt,
            d.CancelledByUserId,
            d.CancelledAt,
            d.CancellationReason);

        return new DeclarationDetailResponse(dto, vehicles);
    }
}
