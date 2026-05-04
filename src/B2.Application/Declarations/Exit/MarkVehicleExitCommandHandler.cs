using System.Globalization;
using System.Text.Json;
using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Shared.Contracts.Declarations;

namespace B2.Application.Declarations.Exit;

public sealed class MarkVehicleExitCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IClock _clock;
    private readonly IAuditContext _audit;

    public MarkVehicleExitCommandHandler(
        IDeclarationRepository declarations,
        IClock clock,
        IAuditContext audit)
    {
        _declarations = declarations;
        _clock = clock;
        _audit = audit;
    }

    public async Task<VehicleExitResponse> HandleAsync(Guid vehicleId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var exitTime = _clock.UtcNow;
        var result = await _declarations.MarkVehicleExitAsync(vehicleId, actorUserId, exitTime, cancellationToken);

        switch (result)
        {
            case MarkVehicleExitApplied a:
            {
                var after = JsonSerializer.Serialize(new { a.ExitTime, DeclarationStatus = a.DeclarationStatus });
                _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(Vehicle), a.VehicleId, null, after, null));
                return new VehicleExitResponse(a.VehicleId, a.ExitTime, a.DeclarationId, a.DeclarationStatus);
            }

            case MarkVehicleExitConflict c:
            {
                var extensions = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["winnerUsername"] = c.WinnerDisplayName,
                    ["exitTime"] = c.ExitTime.ToString("o", CultureInfo.InvariantCulture),
                };
                throw new DomainException(
                    "VEHICLE_ALREADY_EXITED",
                    "Vehicle was already marked exited by another operator.",
                    409,
                    extensions);
            }

            case MarkVehicleExitNotFound:
                throw new DomainException("VEHICLE_NOT_FOUND", "Vehicle not found.", 404);

            default:
                throw new InvalidOperationException("Unexpected mark-exit result.");
        }
    }
}
