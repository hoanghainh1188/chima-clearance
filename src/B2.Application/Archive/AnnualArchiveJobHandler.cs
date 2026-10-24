using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Application.Export;

namespace B2.Application.Archive;

/// <summary>
/// FR-021 / INV-12 — annual archive job handler.  Called by the Quartz
/// <c>AnnualArchiveJob</c> in Infrastructure.
///
/// Algorithm per data-model.md § 6.2:
///   1. Compute cutoff = today − 3 years.
///   2. Enumerate all calendar years that have data older than cutoff.
///   3. For each year: stream export to disk → verify → delete (only if verified).
/// </summary>
public sealed class AnnualArchiveJobHandler
{
    private const int RetentionYears = 3;

    private readonly IAnnualArchiveRepository _archiveRepo;
    private readonly IAnnualArchiveWriter _writer;
    private readonly ExportSoB2QueryHandler _exportHandler;
    private readonly ISoB2StreamingExporter _streamingExporter;
    private readonly IClock _clock;
    private readonly IUnitOfWork _uow;

    public AnnualArchiveJobHandler(
        IAnnualArchiveRepository archiveRepo,
        IAnnualArchiveWriter writer,
        ExportSoB2QueryHandler exportHandler,
        ISoB2StreamingExporter streamingExporter,
        IClock clock,
        IUnitOfWork uow)
    {
        _archiveRepo = archiveRepo;
        _writer = writer;
        _exportHandler = exportHandler;
        _streamingExporter = streamingExporter;
        _clock = clock;
        _uow = uow;
    }

    public async Task<IReadOnlyList<ArchiveYearResult>> HandleAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var cutoff = today.AddYears(-RetentionYears);

        var years = await _archiveRepo.GetYearsOlderThanAsync(cutoff, ct);
        if (years.Count == 0)
            return Array.Empty<ArchiveYearResult>();

        var results = new List<ArchiveYearResult>(years.Count);
        foreach (var year in years)
        {
            var from = new DateOnly(year, 1, 1);
            // For a year fully in the past, archive the whole year.
            // For the cutoff year itself (edge case), archive only dates < cutoff.
            var to = year < cutoff.Year
                ? new DateOnly(year, 12, 31)
                : cutoff.AddDays(-1);

            var count = await _archiveRepo.CountDeclarationsAsync(from, to, ct);

            try
            {
                // #167: stream month-by-month straight to disk — a full year never sits in
                // memory as a workbook DOM, and verification below re-reads it the same way.
                var filePath = await _writer.WriteExcelAsync(
                    year,
                    (stream, token) => _streamingExporter.ExportAsync(
                        _exportHandler.StreamDaysAsync(from, to, token), stream, token),
                    ct);
                var verified = _writer.VerifyExcel(filePath, count);

                if (verified)
                {
                    await _archiveRepo.DeleteAsync(from, to, ct);
                    await _uow.SaveChangesAsync(ct);
                    results.Add(new ArchiveYearResult(year, true, count, null));
                }
                else
                {
                    results.Add(new ArchiveYearResult(
                        year, false, 0,
                        $"Verification failed for {year}: expected ≥ {count} rows in archive file {filePath}"));
                }
            }
            catch (Exception ex)
            {
                results.Add(new ArchiveYearResult(
                    year, false, 0,
                    $"Archive run {year} failed: {ex.Message}"));
            }
        }

        return results;
    }
}
