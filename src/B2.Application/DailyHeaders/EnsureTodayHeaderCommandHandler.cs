using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Domain.DailyHeaders;

namespace B2.Application.DailyHeaders;

public sealed class EnsureTodayHeaderCommandHandler
{
    private readonly IDailyHeaderRepository _dailyHeaders;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public EnsureTodayHeaderCommandHandler(IDailyHeaderRepository dailyHeaders, IClock clock, IUnitOfWork unitOfWork)
    {
        _dailyHeaders = dailyHeaders;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<DailyHeader> HandleAsync(CancellationToken cancellationToken)
    {
        var date = VietnamTime.BusinessDateFromUtc(_clock.UtcNow);
        var existing = await _dailyHeaders.GetByDateAsync(date, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var header = NewHeaderFor(date);
        await _dailyHeaders.AddAsync(header, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return header;
    }

    /// <summary>
    /// Startup catch-up: ensure a header exists for every business day from the
    /// last persisted header through today, so a service that was down across
    /// multiple days does not leave gaps. When no header has ever been written
    /// (fresh install) only today is created. Returns every date in the window
    /// (inclusive) so the caller can recompute each day's summary.
    /// </summary>
    public async Task<IReadOnlyList<DateOnly>> EnsureCatchUpAsync(CancellationToken cancellationToken)
    {
        var today = VietnamTime.BusinessDateFromUtc(_clock.UtcNow);
        var latest = await _dailyHeaders.GetLatestDateAsync(cancellationToken);

        // Backfill from the day after the last header; with no prior headers, just today.
        var start = latest is { } last && last < today ? last.AddDays(1) : today;

        // Safety cap: never backfill more than a year in a single startup pass.
        const int maxBackfillDays = 366;
        if (today.DayNumber - start.DayNumber > maxBackfillDays)
        {
            start = today.AddDays(-maxBackfillDays);
        }

        var window = new List<DateOnly>();
        var created = false;
        for (var date = start; date <= today; date = date.AddDays(1))
        {
            window.Add(date);
            if (await _dailyHeaders.GetByDateAsync(date, cancellationToken) is not null)
            {
                continue;
            }

            await _dailyHeaders.AddAsync(NewHeaderFor(date), cancellationToken);
            created = true;
        }

        if (created)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return window;
    }

    private DailyHeader NewHeaderFor(DateOnly date) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        CreatedAt = _clock.UtcNow,
    };
}
