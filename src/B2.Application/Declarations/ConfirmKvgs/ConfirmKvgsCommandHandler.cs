using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.DomainEvents;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Domain.Users;

namespace B2.Application.Declarations.ConfirmKvgs;

public sealed class ConfirmKvgsCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly ISignalRDomainEventDispatcher _realtime;

    public ConfirmKvgsCommandHandler(
        IDeclarationRepository declarations,
        IClock clock,
        IAuditContext audit,
        ISignalRDomainEventDispatcher realtime)
    {
        _declarations = declarations;
        _clock = clock;
        _audit = audit;
        _realtime = realtime;
    }

    public async Task HandleAsync(ConfirmKvgsCommand command, string? actorRoleClaim, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Role>(actorRoleClaim, ignoreCase: true, out var actorRole))
        {
            throw new DomainException("FORBIDDEN", "Missing or invalid role.", 403);
        }

        var before = JsonSerializer.Serialize(new { Status = nameof(DeclarationStatus.AllExited) });
        var declaration = await _declarations.ConfirmKvgsAsync(
            command.DeclarationId,
            actorRole,
            actorUserId,
            _clock.UtcNow,
            cancellationToken);
        var after = JsonSerializer.Serialize(
            new
            {
                declaration.Status,
                declaration.KvgsConfirmedByUserId,
                declaration.KvgsConfirmedAt,
            });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(Declaration), declaration.Id, before, after, null));
        await _realtime.PublishDeclarationLedgerChangedAsync(declaration.Id, cancellationToken);
    }
}
