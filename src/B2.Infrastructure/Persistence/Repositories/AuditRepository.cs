using B2.Application.Abstractions;
using B2.Application.Admin.Audit;
using B2.Shared.Contracts.Admin;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly B2DbContext _db;

    public AuditRepository(B2DbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditLogItemDto> Items, int Total)> ListAsync(
        AuditLogFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(a => a.EntityType == filter.EntityType);
        }

        if (filter.EntityId is { } entityId)
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (filter.ActorUserId is { } actorId)
        {
            query = query.Where(a => a.ActorUserId == actorId);
        }

        if (filter.Action is { } action)
        {
            query = query.Where(a => a.Action == action);
        }

        // SQLite can't compare or ORDER BY a DateTimeOffset: project (Id, At), then filter, sort
        // and page in memory — same approach as the ledger pagination. Comparing in .NET also
        // compares instants, so a client "from" sent as +07:00 matches rows stored as +00:00
        // (#155; the old SQL text comparison was offset-sensitive).
        var idAndAt = (await query.Select(a => new { a.Id, a.At }).ToListAsync(cancellationToken))
            .Where(x => filter.FromUtc is not { } from || x.At >= from)
            .Where(x => filter.ToUtc is not { } to || x.At <= to)
            .ToList();
        var total = idAndAt.Count;

        // #172: newest first, so page 1 shows what just happened.
        var pageIds = idAndAt
            .OrderByDescending(x => x.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToList();

        if (pageIds.Count == 0)
        {
            return (Array.Empty<AuditLogItemDto>(), total);
        }

        var rows = await _db.AuditLogs.AsNoTracking()
            .Where(a => pageIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var actorIds = rows
            .Where(a => a.ActorUserId != null)
            .Select(a => a.ActorUserId!.Value)
            .Distinct()
            .ToList();
        var usernames = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username, cancellationToken);

        var items = rows
            .OrderByDescending(a => a.At)
            .Select(a => new AuditLogItemDto(
                a.Id,
                a.At,
                a.ActorUserId,
                a.ActorUserId is { } id ? usernames.GetValueOrDefault(id) : null,
                a.Action.ToString(),
                a.EntityType,
                a.EntityId,
                a.ChangesJson,
                a.Reason,
                a.CorrelationId))
            .ToList();

        return (items, total);
    }
}
