namespace B2.Application.Declarations.Note;

public abstract record UpdateVehicleNoteResult;

public sealed record UpdateVehicleNoteApplied(Guid VehicleId, Guid DeclarationId, string? Before, string? After)
    : UpdateVehicleNoteResult;

public sealed record UpdateVehicleNoteNotFound : UpdateVehicleNoteResult;
