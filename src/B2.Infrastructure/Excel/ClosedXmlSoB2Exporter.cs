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

    private static readonly XLColor UnexitedRed = XLColor.Red;
    private static readonly XLColor AllExitedGreen = XLColor.FromArgb(0xC6, 0xEF, 0xCE);
    private static readonly XLColor DayHeaderFill = XLColor.FromArgb(0xD9, 0xE1, 0xF2);

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
    }

    private static void WriteDayHeaderRow(IXLWorksheet ws, int row, SoB2DayBlock day)
    {
        var banner = "Ngày " + day.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(day.OnDutyOfficerName))
        {
            banner += "     Trực chính: " + day.OnDutyOfficerName;
        }

        if (day.TotalVehicles is { } total)
        {
            banner += "     Tổng xe trong ngày: " + total.ToString(CultureInfo.InvariantCulture);
        }

        var range = ws.Range(row, 1, row, LastColumn);
        range.Merge();
        range.Style.Fill.BackgroundColor = DayHeaderFill;
        range.Style.Font.Bold = true;
        ApplyThinBorder(range);
        ws.Cell(row, 1).Value = banner;
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
            ws.Cell(row, 9).Value = FormatTimestamp(vehicle.EntryTime, hasValue: !string.IsNullOrEmpty(vehicle.PlateNumber));
            ws.Cell(row, 10).Value = vehicle.ExitMarked ? "x" : string.Empty;
            ws.Cell(row, 11).Value = vehicle.ExitTime is { } exit ? FormatTimestamp(exit, hasValue: true) : string.Empty;
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

    private static string FormatTimestamp(DateTimeOffset utc, bool hasValue)
    {
        if (!hasValue)
        {
            return string.Empty;
        }

        var local = TimeZoneInfo.ConvertTime(utc, VietnamTime.TimeZone);
        return local.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
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
