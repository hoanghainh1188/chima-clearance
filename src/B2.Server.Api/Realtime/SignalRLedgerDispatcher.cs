using B2.Application.DomainEvents;
using Microsoft.AspNetCore.SignalR;

namespace B2.Server.Api.Realtime;

public sealed class SignalRLedgerDispatcher : ISignalRDomainEventDispatcher
{
    private readonly IHubContext<EventsHub> _hubContext;

    public SignalRLedgerDispatcher(IHubContext<EventsHub> hubContext) => _hubContext = hubContext;

    public Task PublishDeclarationLedgerChangedAsync(Guid declarationId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("LedgerChanged", new { declarationId }, cancellationToken);
}
