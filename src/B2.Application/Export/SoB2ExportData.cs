namespace B2.Application.Export;

/// <summary>The full assembled Sổ B2 export payload for a date range, ready to render.</summary>
public sealed record SoB2ExportData(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<SoB2DayBlock> Days);
