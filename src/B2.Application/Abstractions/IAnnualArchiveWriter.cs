namespace B2.Application.Abstractions;

/// <summary>
/// Writes and verifies the annual archive file on disk.
/// Implemented in Infrastructure so the Application handler can be unit-tested
/// against a stub.
/// </summary>
public interface IAnnualArchiveWriter
{
    /// <summary>
    /// Creates the archive file for <paramref name="year"/>, lets
    /// <paramref name="writeContent"/> stream the workbook into it, and returns the full
    /// file path. The content is written straight to disk, never buffered in memory (#167).
    /// </summary>
    Task<string> WriteExcelAsync(int year, Func<Stream, CancellationToken, Task> writeContent, CancellationToken ct);

    /// <summary>
    /// Re-reads the Excel file at <paramref name="filePath"/> and verifies it can be
    /// parsed and contains at least <paramref name="minimumRows"/> declaration rows.
    /// Returns <see langword="false"/> if the file is unreadable or under-populated.
    /// </summary>
    bool VerifyExcel(string filePath, int minimumRows);
}
