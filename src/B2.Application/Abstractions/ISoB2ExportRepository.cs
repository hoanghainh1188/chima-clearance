using B2.Application.Export;

namespace B2.Application.Abstractions;

/// <summary>
/// Reads declarations (with their vehicles + resolved processor / yard / officer names)
/// for the Sổ B2 export over a business-date range. Cancelled declarations are included
/// (FR-023: they render with strike-through, not omitted).
/// </summary>
public interface ISoB2ExportRepository
{
    Task<IReadOnlyList<SoB2DeclarationBlock>> GetDeclarationsForExportAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken);
}
