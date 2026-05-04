namespace B2.Shared.Contracts.Declarations;

public sealed record VehicleExitResponse(
    Guid VehicleId,
    DateTimeOffset ExitTime,
    Guid DeclarationId,
    string DeclarationStatus);
