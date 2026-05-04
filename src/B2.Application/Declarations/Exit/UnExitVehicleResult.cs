namespace B2.Application.Declarations.Exit;

public abstract record UnExitVehicleResult;

public sealed record UnExitVehicleApplied(Guid VehicleId, Guid DeclarationId, string DeclarationStatus)
    : UnExitVehicleResult;

public sealed record UnExitVehicleNotFound : UnExitVehicleResult;

public sealed record UnExitVehicleInvalidState(string Message) : UnExitVehicleResult;
