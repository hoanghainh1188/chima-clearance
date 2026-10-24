using System.Globalization;
using B2.Application.Abstractions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace B2.Infrastructure.Archive;

/// <summary>
/// FR-021 — writes the annual archive Excel file to disk and verifies it can be opened.
/// PDF output is deferred (requires ADR for a PDF library per Constitution II).
/// Both directions stream (#167): the content goes straight to a file, and verification
/// reads it back one row at a time instead of loading the workbook.
/// </summary>
public sealed class FileSystemArchiveWriter : IAnnualArchiveWriter
{
    // Layout mirrors the Sổ B2 exporters: row 1 is the column-title row; each parent
    // declaration row carries the declaration number in column C. Day-header banner
    // rows and child vehicle rows leave column C empty, so counting non-empty C cells
    // below row 1 yields the number of archived declarations.
    private const uint HeaderTitleRow = 1;
    private const string DeclarationNumberColumn = "C";
    private const int DeclarationNumberColumnIndex = 3;

    private readonly string _archiveBasePath;

    public FileSystemArchiveWriter(string archiveBasePath) =>
        _archiveBasePath = archiveBasePath;

    public async Task<string> WriteExcelAsync(
        int year,
        Func<Stream, CancellationToken, Task> writeContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(writeContent);

        var yearStr = year.ToString(CultureInfo.InvariantCulture);
        var yearDir = Path.Combine(_archiveBasePath, yearStr);
        Directory.CreateDirectory(yearDir);

        var filePath = Path.Combine(yearDir, FormattableString.Invariant($"Số_B2_archive_{year}.xlsx"));
        var tempPath = filePath + ".partial";
        try
        {
            await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                await writeContent(file, ct);
                await file.FlushAsync(ct);
            }

            // A previous run that failed verification left a read-only file behind; replace it.
            if (File.Exists(filePath))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        // Mark read-only per spec (archived files must not be editable via the app).
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        return filePath;
    }

    public bool VerifyExcel(string filePath, int minimumRows)
    {
        try
        {
            // Read-only access works on the read-only file without touching its attributes.
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var document = SpreadsheetDocument.Open(stream, false);

            // FR-021 / INV-12: only delete live data once the archive actually contains
            // at least the number of declarations we exported. A truncated file must fail.
            var count = CountDeclarationRows(document);
            return count is { } rows && rows >= minimumRows;
        }
        catch
        {
            return false;
        }
    }

    private static int? CountDeclarationRows(SpreadsheetDocument document)
    {
        var workbookPart = document.WorkbookPart;
        var sheet = workbookPart?.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault();
        if (workbookPart is null || sheet?.Id?.Value is not { } sheetId)
        {
            return null;
        }

        var sharedStrings = ReadSharedStringPresence(workbookPart.SharedStringTablePart);
        using var reader = OpenXmlReader.Create((WorksheetPart)workbookPart.GetPartById(sheetId));

        var count = 0;
        uint rowNumber = 0;
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.ElementType != typeof(Row) || !reader.IsStartElement)
            {
                reader.Read();
                continue;
            }

            // One row at a time: LoadCurrentElement materialises just this row and advances.
            var row = (Row)reader.LoadCurrentElement()!;
            rowNumber = row.RowIndex?.Value ?? rowNumber + 1;
            if (rowNumber > HeaderTitleRow && HasValue(DeclarationNumberCell(row), sharedStrings))
            {
                count++;
            }
        }

        return count;
    }

    private static Cell? DeclarationNumberCell(Row row)
    {
        var position = 0;
        foreach (var cell in row.Elements<Cell>())
        {
            position++;
            var reference = cell.CellReference?.Value;
            var column = reference is null
                ? position == DeclarationNumberColumnIndex ? DeclarationNumberColumn : null
                : new string(reference.TakeWhile(char.IsLetter).ToArray());
            if (string.Equals(column, DeclarationNumberColumn, StringComparison.OrdinalIgnoreCase))
            {
                return cell;
            }
        }

        return null;
    }

    private static bool HasValue(Cell? cell, IReadOnlyList<bool> sharedStrings)
    {
        if (cell is null)
        {
            return false;
        }

        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return int.TryParse(cell.CellValue?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                && index >= 0 && index < sharedStrings.Count && sharedStrings[index];
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return !string.IsNullOrWhiteSpace(cell.InlineString?.InnerText);
        }

        return !string.IsNullOrWhiteSpace(cell.CellValue?.Text);
    }

    /// <summary>Whether each shared string is non-blank — streamed, one item at a time.</summary>
    private static List<bool> ReadSharedStringPresence(SharedStringTablePart? part)
    {
        var presence = new List<bool>();
        if (part is null)
        {
            return presence;
        }

        using var reader = OpenXmlReader.Create(part);
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.ElementType == typeof(SharedStringItem) && reader.IsStartElement)
            {
                presence.Add(!string.IsNullOrWhiteSpace(reader.LoadCurrentElement()!.InnerText));
            }
            else
            {
                reader.Read();
            }
        }

        return presence;
    }
}
