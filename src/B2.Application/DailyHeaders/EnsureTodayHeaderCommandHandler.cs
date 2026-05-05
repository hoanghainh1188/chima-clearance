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

        var header = new DailyHeader
        {
            Id = Guid.NewGuid(),
            Date = date,
            CreatedAt = _clock.UtcNow,
        };
        await _dailyHeaders.AddAsync(header, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return header;
    }
}
