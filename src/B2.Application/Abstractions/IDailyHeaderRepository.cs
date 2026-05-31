using B2.Domain.DailyHeaders;

namespace B2.Application.Abstractions;

public interface IDailyHeaderRepository
{
    Task<DailyHeader?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    /// <summary>Most recent header date on record, or null when none exist yet.
    /// Drives multi-day startup catch-up (the gap to backfill).</summary>
    Task<DateOnly?> GetLatestDateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyHeader>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);

    Task AddAsync(DailyHeader header, CancellationToken cancellationToken);

    Task<int> CountVehiclesEnteredOnDateAsync(DateOnly date, CancellationToken cancellationToken);

    Task<bool> ExistsForDateAsync(DateOnly date, CancellationToken cancellationToken);
}
