using B2.Application.Abstractions;
using B2.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class ProcessorRepository : IProcessorRepository
{
    private readonly B2DbContext _db;

    public ProcessorRepository(B2DbContext db)
    {
        _db = db;
    }

    public Task<Processor?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Processors.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> FullNameExistsAsync(string fullName, Guid? exceptId, CancellationToken cancellationToken)
    {
        var trimmed = fullName.Trim();
        var q = _db.Processors.AsQueryable().Where(p => p.FullName == trimmed);
        if (exceptId is { } ex)
        {
            q = q.Where(p => p.Id != ex);
        }

        return q.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Processor processor, CancellationToken cancellationToken) =>
        await _db.Processors.AddAsync(processor, cancellationToken);

    public async Task<(IReadOnlyList<Processor> Items, int Total)> ListAsync(
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var q = _db.Processors.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            q = q.Where(p => p.IsActive);
        }

        q = q.OrderByDescending(p => p.IsActive).ThenBy(p => p.FullName);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }
}
