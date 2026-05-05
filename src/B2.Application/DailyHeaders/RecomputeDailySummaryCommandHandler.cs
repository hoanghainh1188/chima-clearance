using B2.Application.Abstractions;
using System.Text.Json;

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
        var isNew = header is null;
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
        var before = JsonSerializer.Serialize(new { totalVehicles = header.TotalVehicles });
        header.TotalVehicles = totalVehicles;
        header.LastSummaryComputedAt = _clock.UtcNow;
        var after = JsonSerializer.Serialize(new { totalVehicles });
        // First-of-day Admin recompute creates the row; subsequent recomputes update it.
        var action = isNew ? Domain.Audit.AuditAction.Create : Domain.Audit.AuditAction.Update;
        _audit.Set(new AuditEnvelope(command.ActorUserId, action, nameof(Domain.DailyHeaders.DailyHeader), header.Id, before, after, command.Reason));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return totalVehicles;
    }
}
