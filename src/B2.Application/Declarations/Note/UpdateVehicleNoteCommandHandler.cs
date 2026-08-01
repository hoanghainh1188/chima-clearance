using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.DomainEvents;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;

namespace B2.Application.Declarations.Note;

/// <summary>
/// FR-003 keeps the audit log append-only, so an edited note writes an <see cref="AuditAction.Update"/>
/// row carrying both the previous and the new text — the ledger stays correctable without losing
/// what it said before.
/// </summary>
public sealed class UpdateVehicleNoteCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IAuditContext _audit;
    private readonly ISignalRDomainEventDispatcher _realtime;

    public UpdateVehicleNoteCommandHandler(
        IDeclarationRepository declarations,
        IAuditContext audit,
        ISignalRDomainEventDispatcher realtime)
    {
        _declarations = declarations;
        _audit = audit;
        _realtime = realtime;
    }

    public async Task HandleAsync(
        UpdateVehicleNoteCommand command,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var result = await _declarations.UpdateVehicleNoteAsync(command.VehicleId, command.Note, cancellationToken);

        switch (result)
        {
            case UpdateVehicleNoteApplied a:
                {
                    var before = JsonSerializer.Serialize(new { Note = a.Before });
                    var after = JsonSerializer.Serialize(new { Note = a.After });
                    _audit.Set(new AuditEnvelope(
                        actorUserId,
                        AuditAction.Update,
                        nameof(Vehicle),
                        a.VehicleId,
                        before,
                        after,
                        null));
                    await _realtime.PublishDeclarationLedgerChangedAsync(a.DeclarationId, cancellationToken);
                    return;
                }

            case UpdateVehicleNoteNotFound:
                throw new DomainException("VEHICLE_NOT_FOUND", "Vehicle not found.", 404);

            default:
                throw new InvalidOperationException("Unexpected note-update result.");
        }
    }
}
