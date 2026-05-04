using B2.Application.Declarations.Exit;
using B2.Domain.Declarations;

namespace B2.Application.Abstractions;

public interface IDeclarationRepository
{
    Task<bool> DeclarationNumberExistsAsync(string declarationNumber, CancellationToken cancellationToken);

    Task AddAsync(Declaration declaration, CancellationToken cancellationToken);

    Task<Declaration?> GetByIdWithVehiclesAsync(Guid id, CancellationToken cancellationToken);

    Task<Declaration?> GetByDeclarationNumberWithVehiclesAsync(string declarationNumber, CancellationToken cancellationToken);

    Task<MarkVehicleExitResult> MarkVehicleExitAsync(
        Guid vehicleId,
        Guid actorUserId,
        DateTimeOffset exitTime,
        CancellationToken cancellationToken);

    Task<UnExitVehicleResult> UnExitVehicleAsync(Guid vehicleId, CancellationToken cancellationToken);
}
