using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.DomainEvents;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Domain.Users;

namespace B2.Application.Declarations.Cancel;

/// <summary>
/// FR-022 — Admin cancels an <see cref="DeclarationStatus.Open"/> declaration with a
/// mandatory reason. Writes a <see cref="AuditAction.Cancel"/> audit row and pushes a
/// ledger-changed event so other clients re-render the row struck-through (FR-023).
/// </summary>
public sealed class CancelDeclarationCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly ISignalRDomainEventDispatcher _realtime;

    public CancelDeclarationCommandHandler(
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

    public async Task HandleAsync(CancelDeclarationCommand command, string? actorRoleClaim, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Role>(actorRoleClaim, ignoreCase: true, out var actorRole))
        {
            throw new DomainException("FORBIDDEN", "Missing or invalid role.", 403);
        }

        var beforeStatus = DeclarationStatus.Open;
        var declaration = await _declarations.CancelDeclarationAsync(
            command.DeclarationId,
            actorRole,
            actorUserId,
            command.Reason,
            _clock.UtcNow,
            cancellationToken);

        var before = JsonSerializer.Serialize(new { Status = beforeStatus });
        var after = JsonSerializer.Serialize(
            new
            {
                declaration.Status,
                declaration.CancelledByUserId,
                declaration.CancelledAt,
                declaration.CancellationReason,
            });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Cancel, nameof(Declaration), declaration.Id, before, after, command.Reason));
        await _realtime.PublishDeclarationLedgerChangedAsync(declaration.Id, cancellationToken);
    }
}
