using B2.Application.Abstractions;
using B2.Domain.DailyHeaders;
using B2.Domain.Declarations;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class DailyHeaderRepository : IDailyHeaderRepository
{
    private readonly B2DbContext _db;

    public DailyHeaderRepository(B2DbContext db) => _db = db;

    public Task<DailyHeader?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken) =>
        _db.DailyHeaders.FirstOrDefaultAsync(x => x.Date == date, cancellationToken);

    public async Task<DateOnly?> GetLatestDateAsync(CancellationToken cancellationToken)
    {
        var dates = _db.DailyHeaders.AsNoTracking().Select(x => x.Date);
        if (!await dates.AnyAsync(cancellationToken))
        {
            return null;
        }

        return await dates.MaxAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyHeader>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        return await _db.DailyHeaders.AsNoTracking()
            .Where(x => x.Date >= fromDate && x.Date <= toDate)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(DailyHeader header, CancellationToken cancellationToken) =>
        _db.DailyHeaders.AddAsync(header, cancellationToken).AsTask();

    public Task<bool> ExistsForDateAsync(DateOnly date, CancellationToken cancellationToken) =>
        _db.DailyHeaders.AnyAsync(x => x.Date == date, cancellationToken);

    public Task<int> CountVehiclesEnteredOnDateAsync(DateOnly date, CancellationToken cancellationToken)
    {
        return _db.Vehicles
            .Where(v => v.Declaration.BusinessDate == date && v.Declaration.Status != DeclarationStatus.Cancelled)
            .CountAsync(cancellationToken);
    }
}
