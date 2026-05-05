using B2.Domain.DailyHeaders;

namespace B2.Application.Abstractions;

public interface IDailyHeaderRepository
{
    Task<DailyHeader?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyHeader>> GetByDateRangeAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);

    Task AddAsync(DailyHeader header, CancellationToken cancellationToken);

    Task<int> CountVehiclesEnteredOnDateAsync(DateOnly date, CancellationToken cancellationToken);

    Task<bool> ExistsForDateAsync(DateOnly date, CancellationToken cancellationToken);
}
