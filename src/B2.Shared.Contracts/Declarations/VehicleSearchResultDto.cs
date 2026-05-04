namespace B2.Shared.Contracts.Declarations;

public sealed record VehicleSearchResultDto(
    Guid VehicleId,
    Guid DeclarationId,
    string PlateNumber,
    string DeclarationNumber,
    string CompanyName,
    DateTimeOffset EntryTime,
    bool ExitMarked);
