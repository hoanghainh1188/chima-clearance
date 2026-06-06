namespace B2.Application.Archive;

/// <summary>Outcome of a single calendar-year archive pass.</summary>
public sealed record ArchiveYearResult(
    int Year,
    bool Success,
    int DeclarationsArchived,
    string? ErrorMessage);
