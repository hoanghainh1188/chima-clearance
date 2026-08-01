namespace B2.Application.Export;

/// <summary>
/// One declaration's block in the Sổ B2 export: a parent row (first vehicle, carrying
/// the declaration metadata in columns A–D, G, H, L–N) followed by child vehicle rows.
/// Status is pre-resolved to booleans so the exporter stays free of domain enums.
/// </summary>
public sealed record SoB2DeclarationBlock(
    DateOnly BusinessDate,
    /// <summary>Primary number — kept for sorting and for callers that need a single value.</summary>
    string DeclarationNumber,
    /// <summary>
    /// Every number on cột C of this row, ordered. The Excel sheet puts them on separate lines
    /// inside the one cell, which is what the legacy file did with Alt+Enter.
    /// </summary>
    IReadOnlyList<string> DeclarationNumbers,
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
