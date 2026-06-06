namespace B2.Application.Abstractions;

/// <summary>
/// Data-access contract for the annual archive job (FR-021 / INV-12).
/// Separates read + delete operations so the handler can be tested without a real DB.
/// </summary>
public interface IAnnualArchiveRepository
{
    /// <summary>
    /// Returns every distinct calendar year for which there are Declarations whose
    /// <c>BusinessDate</c> falls strictly before <paramref name="cutoff"/>.
    /// </summary>
    Task<IReadOnlyList<int>> GetYearsOlderThanAsync(DateOnly cutoff, CancellationToken ct);

    /// <summary>
    /// Counts Declarations whose <c>BusinessDate</c> is within [<paramref name="from"/>,
    /// <paramref name="to"/>] inclusive (used to determine expected archive row count).
    /// </summary>
    Task<int> CountDeclarationsAsync(DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>
    /// Deletes Declarations (+ their Vehicles, linked AuditLog rows, and DailyHeaders
    /// that fall within the same date range) in a single database transaction.
    /// </summary>
    Task DeleteAsync(DateOnly from, DateOnly to, CancellationToken ct);
}
