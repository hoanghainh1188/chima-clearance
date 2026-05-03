using B2.Application.Abstractions;
using B2.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class YardRepository : IYardRepository
{
    private readonly B2DbContext _db;

    public YardRepository(B2DbContext db)
    {
        _db = db;
    }

    public Task<Yard?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Yards.FirstOrDefaultAsync(y => y.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken)
    {
        var c = code.Trim().ToUpperInvariant();
        var q = _db.Yards.AsQueryable().Where(y => y.Code == c);
        if (exceptId is { } ex)
        {
            q = q.Where(y => y.Id != ex);
        }

        return q.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Yard yard, CancellationToken cancellationToken) =>
        await _db.Yards.AddAsync(yard, cancellationToken);

    public async Task<(IReadOnlyList<Yard> Items, int Total)> ListAsync(
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var q = _db.Yards.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            q = q.Where(y => y.IsActive);
        }

        q = q.OrderByDescending(y => y.IsActive).ThenBy(y => y.Code);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }
}
