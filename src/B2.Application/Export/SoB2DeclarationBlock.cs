namespace B2.Application.Export;

/// <summary>
/// One declaration's block in the Sổ B2 export: a parent row (first vehicle, carrying
/// the declaration metadata in columns A–D, G, H, L–N) followed by child vehicle rows.
/// Status is pre-resolved to booleans so the exporter stays free of domain enums.
/// </summary>
public sealed record SoB2DeclarationBlock(
    DateOnly BusinessDate,
    string DeclarationNumber,
    string ProcessorName,
    string CompanyName,
    string GoodsName,
    string YardName,
    bool KvgsConfirmed,
    bool IsAllExited,
    bool IsCancelled,
    string EnteredByName,
    string? KvgsConfirmedByName,
    IReadOnlyList<SoB2VehicleRow> Vehicles);
