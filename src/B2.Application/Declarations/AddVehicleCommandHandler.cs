using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.DomainEvents;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Shared.Contracts.Declarations;

namespace B2.Application.Declarations;

public sealed class AddVehicleCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;
    private readonly ISignalRDomainEventDispatcher _realtime;

    public AddVehicleCommandHandler(
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

    public async Task HandleAsync(
        Guid declarationId,
        VehicleAddRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var plate = PlateNumber.Parse(request.PlateNumber);
        var container = ContainerNumber.TryParse(request.TrailerOrContainerNumber);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var vehicle = await _declarations.AddVehicleAsync(
            declarationId,
            Guid.NewGuid(),
            plate,
            _clock.UtcNow,
            actorUserId,
            container,
            note,
            cancellationToken);

        var after = JsonSerializer.Serialize(new { vehicle.PlateNumber, vehicle.EntryTime });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Create, nameof(Vehicle), vehicle.Id, null, after, null));
        await _realtime.PublishDeclarationLedgerChangedAsync(declarationId, cancellationToken);
    }
}
