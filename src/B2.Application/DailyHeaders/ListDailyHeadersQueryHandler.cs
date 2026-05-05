using B2.Application.Abstractions;
using B2.Shared.Contracts.Declarations;

namespace B2.Application.DailyHeaders;

public sealed class ListDailyHeadersQueryHandler
{
    private readonly IDailyHeaderRepository _dailyHeaders;

    public ListDailyHeadersQueryHandler(IDailyHeaderRepository dailyHeaders) => _dailyHeaders = dailyHeaders;

    public async Task<IReadOnlyList<DailyHeaderDto>> HandleAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var headers = await _dailyHeaders.GetByDateRangeAsync(fromDate, toDate, cancellationToken);
        return headers
            .Select(h => new DailyHeaderDto(h.Date, h.OnDutyOfficerName, h.TotalVehicles, h.LastSummaryComputedAt))
            .ToList();
    }
}
