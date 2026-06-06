namespace B2.Application.Abstractions;

/// <summary>
/// Writes and verifies the annual archive file on disk.
/// Implemented in Infrastructure so the Application handler can be unit-tested
/// against a stub.
/// </summary>
public interface IAnnualArchiveWriter
{
    /// <summary>
    /// Persists <paramref name="excelBytes"/> to the archive folder for
    /// <paramref name="year"/> and returns the full file path.
    /// </summary>
    Task<string> WriteExcelAsync(int year, byte[] excelBytes, CancellationToken ct);

    /// <summary>
    /// Re-opens the Excel file at <paramref name="filePath"/> and verifies it can be
    /// parsed and contains at least <paramref name="minimumRows"/> data rows.
    /// Returns <see langword="false"/> if the file is unreadable or under-populated.
    /// </summary>
    bool VerifyExcel(string filePath, int minimumRows);
}
