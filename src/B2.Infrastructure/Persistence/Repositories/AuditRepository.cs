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

        if (filter.FromUtc is { } from)
        {
            query = query.Where(a => a.At >= from);
        }

        if (filter.ToUtc is { } to)
        {
            query = query.Where(a => a.At <= to);
        }

        // SQLite can't ORDER BY a DateTimeOffset: project (Id, At), sort + page in memory,
        // then fetch the page's full rows — same approach as the ledger pagination.
        var idAndAt = await query.Select(a => new { a.Id, a.At }).ToListAsync(cancellationToken);
        var total = idAndAt.Count;

        var pageIds = idAndAt
            .OrderBy(x => x.At)
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
            .OrderBy(a => a.At)
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
