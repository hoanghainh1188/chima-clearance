using B2.Application.Abstractions;
using System.Globalization;

namespace B2.Application.DailyHeaders;

public sealed class RecomputeDailySummaryCommandHandler
{
    private readonly IDailyHeaderRepository _dailyHeaders;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;

    public RecomputeDailySummaryCommandHandler(
        IDailyHeaderRepository dailyHeaders,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit)
    {
        _dailyHeaders = dailyHeaders;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<int> HandleAsync(RecomputeDailySummaryCommand command, CancellationToken cancellationToken)
    {
        var header = await _dailyHeaders.GetByDateAsync(command.Date, cancellationToken);
        if (header is null)
        {
            header = new Domain.DailyHeaders.DailyHeader
            {
                Id = Guid.NewGuid(),
                Date = command.Date,
                CreatedAt = _clock.UtcNow,
            };
            await _dailyHeaders.AddAsync(header, cancellationToken);
        }

        var totalVehicles = await _dailyHeaders.CountVehiclesEnteredOnDateAsync(command.Date, cancellationToken);
        var before = $$"""{"totalVehicles":{{(header.TotalVehicles?.ToString(CultureInfo.InvariantCulture) ?? "null")}}}""";
        header.TotalVehicles = totalVehicles;
        header.LastSummaryComputedAt = _clock.UtcNow;
        var after = $$"""{"totalVehicles":{{totalVehicles}}}""";
        _audit.Set(new AuditEnvelope(command.ActorUserId, Domain.Audit.AuditAction.Update, nameof(Domain.DailyHeaders.DailyHeader), header.Id, before, after, command.Reason));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return totalVehicles;
    }
}
