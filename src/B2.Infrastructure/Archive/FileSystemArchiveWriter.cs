using System.Globalization;
using B2.Application.Abstractions;
using ClosedXML.Excel;

namespace B2.Infrastructure.Archive;

/// <summary>
/// FR-021 — writes the annual archive Excel file to disk and verifies it can be opened.
/// PDF output is deferred (requires ADR for a PDF library per Constitution II).
/// </summary>
public sealed class FileSystemArchiveWriter : IAnnualArchiveWriter
{
    private readonly string _archiveBasePath;

    public FileSystemArchiveWriter(string archiveBasePath) =>
        _archiveBasePath = archiveBasePath;

    public async Task<string> WriteExcelAsync(int year, byte[] excelBytes, CancellationToken ct)
    {
        var yearStr = year.ToString(CultureInfo.InvariantCulture);
        var yearDir = Path.Combine(_archiveBasePath, yearStr);
        Directory.CreateDirectory(yearDir);

        var filePath = Path.Combine(yearDir, FormattableString.Invariant($"Số_B2_archive_{year}.xlsx"));
        await File.WriteAllBytesAsync(filePath, excelBytes, ct);

        // Mark read-only per spec (archived files must not be editable via the app).
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        return filePath;
    }

    public bool VerifyExcel(string filePath, int minimumRows)
    {
        try
        {
            // Re-open with ClosedXML to confirm the workbook is readable and non-empty.
            // Temporarily clear ReadOnly so ClosedXML's stream-based open works on some systems.
            var attrs = File.GetAttributes(filePath);
            File.SetAttributes(filePath, attrs & ~FileAttributes.ReadOnly);

            using var wb = new XLWorkbook(filePath);
            File.SetAttributes(filePath, attrs); // restore read-only

            var ws = wb.Worksheets.FirstOrDefault();
            if (ws is null)
                return false;

            // Sổ B2 layout: row 1 = column titles; each declaration occupies the
            // parent row with a declaration number in column C (child vehicle rows leave C blank).
            var declarationRows = ws.RowsUsed()
                .Skip(1)
                .Count(row => !string.IsNullOrWhiteSpace(row.Cell(3).GetString()));

            return declarationRows >= minimumRows;
        }
        catch
        {
            return false;
        }
    }
}
