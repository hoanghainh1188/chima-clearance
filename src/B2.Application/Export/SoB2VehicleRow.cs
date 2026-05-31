namespace B2.Application.Export;

/// <summary>One physical vehicle line in the Sổ B2 export (columns E, F, I, J, K, O).</summary>
public sealed record SoB2VehicleRow(
    string PlateNumber,
    string? TrailerOrContainerNumber,
    DateTimeOffset EntryTime,
    bool ExitMarked,
    DateTimeOffset? ExitTime,
    string? Note);
