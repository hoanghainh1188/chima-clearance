using B2.Application.DomainEvents;
using Microsoft.AspNetCore.SignalR;

namespace B2.Server.Api.Realtime;

public sealed class SignalRLedgerDispatcher : ISignalRDomainEventDispatcher
{
    private readonly IHubContext<EventsHub> _hubContext;

    public SignalRLedgerDispatcher(IHubContext<EventsHub> hubContext) => _hubContext = hubContext;

    public Task PublishDeclarationLedgerChangedAsync(Guid declarationId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("LedgerChanged", new { declarationId }, cancellationToken);

    public Task PublishDailyHeaderOfficerChangedAsync(DateOnly date, string officerName, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync(
            "DailyHeaderOfficerChanged",
            new { date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), officerName },
            cancellationToken);
}
