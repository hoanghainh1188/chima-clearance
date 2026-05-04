using System.Text.Json;
using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;

namespace B2.Application.Declarations.Exit;

public sealed class UnExitVehicleCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IAuditContext _audit;

    public UnExitVehicleCommandHandler(IDeclarationRepository declarations, IAuditContext audit)
    {
        _declarations = declarations;
        _audit = audit;
    }

    public async Task HandleAsync(Guid vehicleId, Guid actorUserId, string reason, CancellationToken cancellationToken)
    {
        var result = await _declarations.UnExitVehicleAsync(vehicleId, cancellationToken);

        switch (result)
        {
            case UnExitVehicleApplied a:
                {
                    var after = JsonSerializer.Serialize(new { a.DeclarationStatus, ExitMarked = false });
                    _audit.Set(new AuditEnvelope(actorUserId, AuditAction.UnExit, nameof(Vehicle), a.VehicleId, null, after, reason));
                    return;
                }

            case UnExitVehicleNotFound:
                throw new DomainException("VEHICLE_NOT_FOUND", "Vehicle not found.", 404);

            case UnExitVehicleInvalidState x:
                throw new DomainException("VEHICLE_NOT_EXITED", x.Message, 400);

            default:
                throw new InvalidOperationException("Unexpected un-exit result.");
        }
    }
}
