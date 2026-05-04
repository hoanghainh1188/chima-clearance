namespace B2.Application.Declarations.Exit;

public abstract record MarkVehicleExitResult;

public sealed record MarkVehicleExitApplied(
    Guid VehicleId,
    DateTimeOffset ExitTime,
    Guid DeclarationId,
    string DeclarationStatus) : MarkVehicleExitResult;

public sealed record MarkVehicleExitConflict(string WinnerDisplayName, DateTimeOffset ExitTime) : MarkVehicleExitResult;

public sealed record MarkVehicleExitNotFound : MarkVehicleExitResult;
