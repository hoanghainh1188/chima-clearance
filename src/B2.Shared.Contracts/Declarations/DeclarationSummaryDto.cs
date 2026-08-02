namespace B2.Shared.Contracts.Declarations;

public sealed record DeclarationSummaryDto(
    Guid Id,
    string DeclarationNumber,
    string CompanyName,
    string GoodsName,
    string Status,
    int VehicleCount,
    int UnexitedVehicleCount,
    Guid ProcessorId,
    string ProcessorName,
    Guid YardId,
    string YardCode,
    string YardName,
    Guid EnteredByUserId,
    string EnteredByDisplayName,
    DateTimeOffset EnteredAt,
    DateOnly BusinessDate,
    Guid? KvgsConfirmedByUserId,
    DateTimeOffset? KvgsConfirmedAt,
    string PrimaryPlateNumber,
    string? PrimaryTrailerOrContainer,
    /// <summary>How many numbers sit on cột C of this row; 1 for the common case.</summary>
    int DeclarationNumberCount);
