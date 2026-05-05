using B2.Application.Abstractions;
using B2.Application.DomainEvents;
using System.Text.Json;

namespace B2.Application.DailyHeaders;

public sealed class UpdateOfficerNameCommandHandler
{
    private readonly IDailyHeaderRepository _dailyHeaders;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;
    private readonly ISignalRDomainEventDispatcher _realtime;

    public UpdateOfficerNameCommandHandler(
        IDailyHeaderRepository dailyHeaders,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit,
        ISignalRDomainEventDispatcher realtime)
    {
        _dailyHeaders = dailyHeaders;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _realtime = realtime;
    }

    public async Task HandleAsync(UpdateOfficerNameCommand command, CancellationToken cancellationToken)
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

        var before = JsonSerializer.Serialize(new { officerName = header.OnDutyOfficerName });
        var trimmed = command.Name.Trim();
        header.OnDutyOfficerName = trimmed;
        var after = JsonSerializer.Serialize(new { officerName = trimmed });
        // Audit the row's existence delta accurately: Create on first PATCH-of-the-day
        // (cron didn't fire / catch-up didn't run); OfficerNameChanged on subsequent edits.
        var action = isNew ? Domain.Audit.AuditAction.Create : Domain.Audit.AuditAction.OfficerNameChanged;
        _audit.Set(new AuditEnvelope(command.ActorUserId, action, nameof(Domain.DailyHeaders.DailyHeader), header.Id, before, after, null));
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _realtime.PublishDailyHeaderOfficerChangedAsync(command.Date, trimmed, cancellationToken);
    }
}
