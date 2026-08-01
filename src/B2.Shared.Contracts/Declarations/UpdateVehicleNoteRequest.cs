namespace B2.Shared.Contracts.Declarations;

/// <summary>Ghi chú (cột O) — an empty or whitespace value clears the note.</summary>
public sealed record UpdateVehicleNoteRequest(string? Note);
