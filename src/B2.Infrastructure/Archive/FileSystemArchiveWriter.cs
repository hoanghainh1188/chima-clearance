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

            // Parent declaration rows carry the declaration number in column C (see
            // ClosedXmlSoB2Exporter). Day-header banners and child vehicle rows omit it.
            var declarationRows = CountDeclarationParentRows(ws);

            // INV-12: refuse delete when the archive is missing declarations we counted
            // in the live DB. An empty year (minimumRows == 0) only needs a readable file.
            return minimumRows == 0
                ? declarationRows == 0
                : declarationRows >= minimumRows;
        }
        catch
        {
            return false;
        }
    }

    private static int CountDeclarationParentRows(IXLWorksheet ws)
    {
        const int firstDataRow = 2;
        const int declarationNumberColumn = 3;

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRow < firstDataRow)
        {
            return 0;
        }

        var count = 0;
        for (var row = firstDataRow; row <= lastRow; row++)
        {
            var declarationNumber = ws.Cell(row, declarationNumberColumn).GetString();
            if (!string.IsNullOrEmpty(declarationNumber))
            {
                count++;
            }
        }

        return count;
    }
}
