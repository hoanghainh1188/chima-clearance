namespace B2.Shared.Contracts.Declarations;

public sealed record VehicleDetailDto(
    Guid Id,
    string PlateNumber,
    string? TrailerOrContainerNumber,
    DateTimeOffset EntryTime,
    bool ExitMarked,
    DateTimeOffset? ExitTime);
