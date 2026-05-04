namespace B2.Application.Declarations.List;

/// <summary>Projection row for ledger list (mapped to <see cref="B2.Shared.Contracts.Declarations.DeclarationSummaryDto"/>).</summary>
public sealed record DeclarationSummaryRow(
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
    string? PrimaryTrailerOrContainer);
