namespace B2.Application.Declarations.Note;

/// <summary>Business rules § 3 — "Ghi chú" (cột O) is editable at the exit gate.</summary>
public sealed record UpdateVehicleNoteCommand(Guid VehicleId, string? Note);
