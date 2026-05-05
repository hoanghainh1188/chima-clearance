namespace B2.Application.DomainEvents;

/// <summary>
/// Pushes ledger-related updates to connected desktop clients (SignalR implementation lives in B2.Server.Api).
/// </summary>
public interface ISignalRDomainEventDispatcher
{
    Task PublishDeclarationLedgerChangedAsync(Guid declarationId, CancellationToken cancellationToken = default);

    Task PublishDailyHeaderOfficerChangedAsync(DateOnly date, string officerName, CancellationToken cancellationToken = default);
}
