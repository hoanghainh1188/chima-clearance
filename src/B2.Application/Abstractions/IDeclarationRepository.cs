using B2.Application.Declarations.Exit;
using B2.Application.Declarations.List;
using B2.Domain.Declarations;
using B2.Domain.Users;
using B2.Shared.Contracts.Declarations;

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

    Task<Declaration> ConfirmKvgsAsync(
        Guid declarationId,
        Role actorRole,
        Guid actorUserId,
        DateTimeOffset confirmedAtUtc,
        CancellationToken cancellationToken);

    Task<Vehicle> AddVehicleAsync(
        Guid declarationId,
        Guid vehicleId,
        PlateNumber plate,
        DateTimeOffset entryTime,
        Guid enteredByUserId,
        ContainerNumber? container,
        string? note,
        CancellationToken cancellationToken);

    Task<Declaration> CancelDeclarationAsync(
        Guid declarationId,
        Role actorRole,
        Guid actorUserId,
        string reason,
        DateTimeOffset cancelledAtUtc,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<DeclarationSummaryRow> Items, int TotalCount)> ListDeclarationsAsync(
        ListDeclarationsRepositoryQuery query,
        CancellationToken cancellationToken);

    Task<DeclarationDetailResponse?> GetDeclarationDetailAsync(Guid id, CancellationToken cancellationToken);
}
