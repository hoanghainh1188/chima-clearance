namespace B2.Shared.Contracts.Declarations;

public sealed record VehicleAddRequest(
    string PlateNumber,
    string? TrailerOrContainerNumber,
    string? Note);
