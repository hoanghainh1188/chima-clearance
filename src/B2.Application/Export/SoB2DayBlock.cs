namespace B2.Application.Export;

/// <summary>
/// One day's block: the daily-header row (date, on-duty officer, total vehicles) followed
/// by that day's declaration blocks. Mirrors the legacy "dòng chính" inserted at 00:01.
/// </summary>
public sealed record SoB2DayBlock(
    DateOnly Date,
    string? OnDutyOfficerName,
    int? TotalVehicles,
    IReadOnlyList<SoB2DeclarationBlock> Declarations);
