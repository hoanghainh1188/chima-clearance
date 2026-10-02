using System.Globalization;
using System.Reflection;
using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Application.Export;
using ClosedXML.Excel;

namespace B2.Infrastructure.Excel;

/// <summary>
/// F-13 — renders the Sổ B2 export from the legacy <c>Mẫu sổ B2.xlsx</c> template
/// (FR-015: preserved column order A–O, daily-header rows, conditional colouring).
/// The template's row 1 (column titles) and column widths are inherited; the sample
/// data below it is cleared and replaced with the real rows.
/// </summary>
public sealed class ClosedXmlSoB2Exporter : ISoB2Exporter
{
    private const int HeaderTitleRow = 1;
    private const int FirstDataRow = 2;
    private const int LastColumn = 15; // A..O
    private const int DeclarationNumberColumn = 3; // C

    /// <summary>Cột P, row 1: the legacy "Tìm xe" input box; the template ships a sample value.</summary>
    private const int SearchBoxColumn = 16;

    /// <summary>Display form from business rules cột I/K: "26/2/2026 13:16".</summary>
    private const string TimestampFormat = "d/M/yyyy H:mm";

    private static readonly XLColor UnexitedRed = XLColor.Red;
    private static readonly XLColor AllExitedGreen = XLColor.FromArgb(0xC6, 0xEF, 0xCE);
    private static readonly XLColor DayHeaderFill = XLColor.FromArgb(0xD9, 0xE1, 0xF2);

    // Excel's "light red fill with dark red text" — what the template's duplicate rule uses.
    private static readonly XLColor DuplicateText = XLColor.FromArgb(0x9C, 0x00, 0x06);
    private static readonly XLColor DuplicateFill = XLColor.FromArgb(0xFF, 0xC7, 0xCE);

    private readonly string? _onDiskTemplatePath;

    /// <param name="dataDirectory">
    /// ProgramData root. If <c>{dataDirectory}/templates/Mẫu sổ B2.xlsx</c> exists it
    /// overrides the embedded template (lets the unit re-skin the export without a redeploy).
    /// </param>
    public ClosedXmlSoB2Exporter(string dataDirectory)
    {
        var candidate = Path.Combine(dataDirectory, "templates", "Mẫu sổ B2.xlsx");
        _onDiskTemplatePath = File.Exists(candidate) ? candidate : null;
    }

    public byte[] Export(SoB2ExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var template = OpenTemplate();
        using var workbook = new XLWorkbook(template);
        var ws = workbook.Worksheet(1);

        ClearSampleRows(ws);

        var row = FirstDataRow;
        var stt = 0;
        foreach (var day in data.Days)
        {
            WriteDayHeaderRow(ws, row, day);
            row++;

            foreach (var declaration in day.Declarations)
            {
                stt++;
                row = WriteDeclarationBlock(ws, row, stt, declaration);
            }
        }

        HighlightDuplicateDeclarationNumbers(ws, row - 1);

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static void ClearSampleRows(IXLWorksheet ws)
    {
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? HeaderTitleRow;
        if (lastRow >= FirstDataRow)
        {
            ws.Rows(FirstDataRow, lastRow).Delete();
        }

        // The template's sample "Tìm xe" value (1358) is not data; keep the cell's styling only.
        ws.Cell(HeaderTitleRow, SearchBoxColumn).Clear(XLClearOptions.Contents);
    }

    /// <summary>
    /// Business rules cột C: a duplicated declaration number is flagged red. The template's
    /// duplicate rule was split across its sample rows, so deleting them shrank it to C1;
    /// re-create one rule over every exported row, in the template's colours.
    /// </summary>
    private static void HighlightDuplicateDeclarationNumbers(IXLWorksheet ws, int lastDataRow)
    {
        ws.ConditionalFormats.RemoveAll();
        if (lastDataRow < FirstDataRow)
        {
            return;
        }

        var duplicates = ws.Range(FirstDataRow, DeclarationNumberColumn, lastDataRow, DeclarationNumberColumn)
            .AddConditionalFormat()
            .WhenIsDuplicate();
        duplicates.Font.SetFontColor(DuplicateText);
        duplicates.Fill.SetBackgroundColor(DuplicateFill);
    }

    /// <summary>
    /// The daily row, laid out exactly as the unit's own file does it: ngày in cột D, tổng số xe
    /// in cột E <b>as a number</b>, người trực chính in cột F. Deliberately not merged — merging
    /// A:O (the earlier shape) turned the total into part of a sentence, so it could no longer be
    /// summed, filtered or sorted by column.
    /// </summary>
    private static void WriteDayHeaderRow(IXLWorksheet ws, int row, SoB2DayBlock day)
    {
        var range = ws.Range(row, 1, row, LastColumn);
        range.Style.Fill.BackgroundColor = DayHeaderFill;
        range.Style.Font.Bold = true;
        ApplyThinBorder(range);

        ws.Cell(row, 4).Value = "Ngày " + day.Date.ToString("dd/M/yyyy", CultureInfo.InvariantCulture);

        if (day.TotalVehicles is { } total)
        {
            ws.Cell(row, 5).Value = total;
        }

        if (!string.IsNullOrWhiteSpace(day.OnDutyOfficerName))
        {
            ws.Cell(row, 6).Value = day.OnDutyOfficerName;
        }
    }

    private static int WriteDeclarationBlock(IXLWorksheet ws, int row, int stt, SoB2DeclarationBlock declaration)
    {
        // A declaration always renders at least one row even with no vehicle attached.
        var vehicles = declaration.Vehicles.Count > 0
            ? declaration.Vehicles
            : new List<SoB2VehicleRow> { new(string.Empty, null, default, false, null, null) };

        var firstRow = row;
        for (var i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];
            var isParent = i == 0;

            if (isParent)
            {
                ws.Cell(row, 1).Value = stt;
                ws.Cell(row, 2).Value = declaration.ProcessorName;
                // Cột C: several numbers live in ONE cell on separate lines, exactly as the
                // legacy file did with Alt+Enter. WrapText makes the breaks visible in Excel 2010.
                var numbers = declaration.DeclarationNumbers is { Count: > 0 }
                    ? declaration.DeclarationNumbers
                    : new[] { declaration.DeclarationNumber };
                ws.Cell(row, 3).Value = string.Join(Environment.NewLine, numbers);
                if (numbers.Count > 1)
                {
                    ws.Cell(row, 3).Style.Alignment.WrapText = true;
                }
                ws.Cell(row, 4).Value = declaration.CompanyName;
                ws.Cell(row, 7).Value = declaration.GoodsName;
                ws.Cell(row, 8).Value = declaration.YardName;
                ws.Cell(row, 12).Value = declaration.KvgsConfirmed ? "x" : string.Empty;
                ws.Cell(row, 13).Value = declaration.EnteredByName;
                ws.Cell(row, 14).Value = declaration.KvgsConfirmedByName ?? string.Empty;
            }

            ws.Cell(row, 5).Value = vehicle.PlateNumber;
            ws.Cell(row, 6).Value = vehicle.TrailerOrContainerNumber ?? string.Empty;
            WriteTimestamp(
                ws,
                row,
                9,
                string.IsNullOrEmpty(vehicle.PlateNumber) ? null : vehicle.EntryTime);
            ws.Cell(row, 10).Value = vehicle.ExitMarked ? "x" : string.Empty;
            WriteTimestamp(ws, row, 11, vehicle.ExitTime);
            ws.Cell(row, 15).Value = vehicle.Note ?? string.Empty;

            var rowRange = ws.Range(row, 1, row, LastColumn);
            ApplyThinBorder(rowRange);

            // Red text for vehicles still inside (legacy rule "tô chữ màu đỏ").
            if (!vehicle.ExitMarked && !string.IsNullOrEmpty(vehicle.PlateNumber))
            {
                rowRange.Style.Font.FontColor = UnexitedRed;
            }

            row++;
        }

        // Green fill on the parent row once every vehicle has exited (ready for KVGS).
        if (declaration.IsAllExited)
        {
            ws.Range(firstRow, 1, firstRow, LastColumn).Style.Fill.BackgroundColor = AllExitedGreen;
        }

        // Strike-through across the whole block for a cancelled declaration (FR-023).
        if (declaration.IsCancelled)
        {
            ws.Range(firstRow, 1, row - 1, LastColumn).Style.Font.Strikethrough = true;
        }

        return row;
    }

    /// <summary>
    /// Cột I / cột K hold a real date-time value, not text. The unit's file stores them as Excel
    /// serial numbers with a display format, and that is what makes the column sortable and
    /// subtractable; writing "01/08/2026 23:04" as a string sorts alphabetically instead.
    /// Display form is the one named in the business rules: "26/2/2026 13:16".
    /// </summary>
    private static void WriteTimestamp(IXLWorksheet ws, int row, int column, DateTimeOffset? utc)
    {
        var cell = ws.Cell(row, column);
        if (utc is not { } value)
        {
            cell.Value = string.Empty;
            return;
        }

        cell.Value = TimeZoneInfo.ConvertTime(value, VietnamTime.TimeZone).DateTime;
        cell.Style.DateFormat.Format = TimestampFormat;
    }

    private static void ApplyThinBorder(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private Stream OpenTemplate()
    {
        if (_onDiskTemplatePath is not null)
        {
            return File.OpenRead(_onDiskTemplatePath);
        }

        var assembly = typeof(ClosedXmlSoB2Exporter).Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("MauSoB2.xlsx", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Embedded Sổ B2 template 'MauSoB2.xlsx' was not found.");

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded Sổ B2 template stream could not be opened.");
    }
}
