using System.Runtime.CompilerServices;
using B2.Application.Abstractions;

namespace B2.Application.Export;

/// <summary>
/// F-13 — assembles the Sổ B2 export for a business-date range: groups declarations by
/// day, merges each day's persisted daily-header (officer + total), and hands the result
/// to <see cref="ISoB2Exporter"/>. A day with a header but no declarations still produces
/// a header row; an empty range yields a valid header-only / empty file.
/// </summary>
public sealed class ExportSoB2QueryHandler
{
    private readonly ISoB2ExportRepository _exportRepository;
    private readonly IDailyHeaderRepository _dailyHeaders;
    private readonly ISoB2Exporter _exporter;

    public ExportSoB2QueryHandler(
        ISoB2ExportRepository exportRepository,
        IDailyHeaderRepository dailyHeaders,
        ISoB2Exporter exporter)
    {
        _exportRepository = exportRepository;
        _dailyHeaders = dailyHeaders;
        _exporter = exporter;
    }

    public async Task<byte[]> HandleAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var days = await LoadDaysAsync(fromDate, toDate, cancellationToken);
        return _exporter.Export(new SoB2ExportData(fromDate, toDate, days));
    }

    /// <summary>
    /// The same day blocks as <see cref="HandleAsync"/>, loaded one calendar month at a time so
    /// a full-year archive (FR-021) never holds more than a month of rows in memory (#167).
    /// Days never straddle a month, so the concatenation equals a single-range load.
    /// </summary>
    public async IAsyncEnumerable<SoB2DayBlock> StreamDaysAsync(
        DateOnly fromDate,
        DateOnly toDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var monthStart = fromDate;
        while (monthStart <= toDate)
        {
            var nextMonth = new DateOnly(monthStart.Year, monthStart.Month, 1).AddMonths(1);
            var monthEnd = nextMonth.AddDays(-1) < toDate ? nextMonth.AddDays(-1) : toDate;

            foreach (var day in await LoadDaysAsync(monthStart, monthEnd, cancellationToken))
            {
                yield return day;
            }

            monthStart = nextMonth;
        }
    }

    private async Task<List<SoB2DayBlock>> LoadDaysAsync(DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var declarations = await _exportRepository.GetDeclarationsForExportAsync(fromDate, toDate, cancellationToken);
        var headers = await _dailyHeaders.GetByDateRangeAsync(fromDate, toDate, cancellationToken);

        var headerByDate = headers.ToDictionary(h => h.Date);
        var declarationsByDate = declarations
            .GroupBy(d => d.BusinessDate)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SoB2DeclarationBlock>)g.ToList());

        // Emit a day block for any date in range that has either a header or declarations.
        var dates = new SortedSet<DateOnly>();
        foreach (var h in headers)
        {
            dates.Add(h.Date);
        }

        foreach (var d in declarations)
        {
            dates.Add(d.BusinessDate);
        }

        var days = new List<SoB2DayBlock>(dates.Count);
        foreach (var date in dates)
        {
            headerByDate.TryGetValue(date, out var header);
            var dayDeclarations = declarationsByDate.GetValueOrDefault(date, Array.Empty<SoB2DeclarationBlock>());
            days.Add(new SoB2DayBlock(date, header?.OnDutyOfficerName, header?.TotalVehicles, dayDeclarations));
        }

        return days;
    }
}
