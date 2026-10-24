using System.Globalization;
using B2.Application.Common;
using B2.Application.Export;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace B2.Infrastructure.Excel;

/// <summary>
/// Emits the Sổ B2 data rows (day header + declaration blocks) to an open
/// <c>&lt;sheetData&gt;</c>, cell for cell what <see cref="ClosedXmlSoB2Exporter"/> writes:
/// values in the same columns, strings inline (no shared-string table to hold in memory),
/// timestamps as Excel serial numbers. Only the current row exists as objects.
/// </summary>
internal sealed class SheetRowWriter
{
    private const int LastColumn = 15; // A..O

    private static readonly string[] ColumnLetters =
        Enumerable.Range(0, LastColumn).Select(i => ((char)('A' + i)).ToString()).ToArray();

    private readonly OpenXmlWriter _writer;
    private readonly SoB2StyleSheet _styles;
    private readonly uint[] _columnStyles;
    private int _stt;

    public SheetRowWriter(OpenXmlWriter writer, SoB2StyleSheet styles, uint[] columnStyles)
    {
        _writer = writer;
        _styles = styles;
        _columnStyles = columnStyles;
        LastRow = 1; // row 1 is the template's column-title row
    }

    /// <summary>Index of the last row written so far.</summary>
    public uint LastRow { get; private set; }

    public void WriteDay(SoB2DayBlock day)
    {
        WriteDayHeaderRow(day);
        foreach (var declaration in day.Declarations)
        {
            _stt++;
            WriteDeclarationBlock(declaration);
        }
    }

    /// <summary>Ngày in cột D, tổng số xe in cột E as a number, người trực chính in cột F; not merged.</summary>
    private void WriteDayHeaderRow(SoB2DayBlock day)
    {
        var values = new object?[LastColumn + 1];
        values[4] = "Ngày " + day.Date.ToString("dd/M/yyyy", CultureInfo.InvariantCulture);
        values[5] = day.TotalVehicles;
        values[6] = string.IsNullOrWhiteSpace(day.OnDutyOfficerName) ? null : day.OnDutyOfficerName;

        WriteRow(values, _ => SoB2CellLook.DayHeaderFill | SoB2CellLook.Bold | SoB2CellLook.ThinBorder);
    }

    private void WriteDeclarationBlock(SoB2DeclarationBlock declaration)
    {
        // A declaration always renders at least one row even with no vehicle attached.
        var vehicles = declaration.Vehicles.Count > 0
            ? declaration.Vehicles
            : new List<SoB2VehicleRow> { new(string.Empty, null, default, false, null, null) };

        for (var i = 0; i < vehicles.Count; i++)
        {
            var vehicle = vehicles[i];
            var isParent = i == 0;
            var values = new object?[LastColumn + 1];
            var multipleNumbers = false;

            if (isParent)
            {
                var numbers = declaration.DeclarationNumbers is { Count: > 0 }
                    ? declaration.DeclarationNumbers
                    : new[] { declaration.DeclarationNumber };
                multipleNumbers = numbers.Count > 1;
                values[1] = _stt;
                values[2] = declaration.ProcessorName;
                values[3] = string.Join(Environment.NewLine, numbers);
                values[4] = declaration.CompanyName;
                values[7] = declaration.GoodsName;
                values[8] = declaration.YardName;
                values[12] = declaration.KvgsConfirmed ? "x" : null;
                values[13] = declaration.EnteredByName;
                values[14] = declaration.KvgsConfirmedByName;
            }

            values[5] = vehicle.PlateNumber;
            values[6] = vehicle.TrailerOrContainerNumber;
            values[9] = string.IsNullOrEmpty(vehicle.PlateNumber) ? null : ToExcelTime(vehicle.EntryTime);
            values[10] = vehicle.ExitMarked ? "x" : null;
            values[11] = vehicle.ExitTime is { } exit ? ToExcelTime(exit) : null;
            values[15] = vehicle.Note;

            var rowLook = SoB2CellLook.ThinBorder;
            if (!vehicle.ExitMarked && !string.IsNullOrEmpty(vehicle.PlateNumber))
            {
                rowLook |= SoB2CellLook.RedText; // legacy rule "tô chữ màu đỏ"
            }

            if (isParent && declaration.IsAllExited)
            {
                rowLook |= SoB2CellLook.AllExitedFill; // ready for KVGS
            }

            if (declaration.IsCancelled)
            {
                rowLook |= SoB2CellLook.Strikethrough; // FR-023
            }

            WriteRow(values, column =>
            {
                var look = rowLook;
                if (column == 3 && multipleNumbers)
                {
                    look |= SoB2CellLook.WrapText;
                }

                if (values[column] is double)
                {
                    look |= SoB2CellLook.Timestamp;
                }

                return look;
            });
        }
    }

    /// <param name="values">1-based by column: string, int (number) or double (timestamp serial).</param>
    private void WriteRow(object?[] values, Func<int, SoB2CellLook> lookOf)
    {
        LastRow++;
        var rowNumber = LastRow.ToString(CultureInfo.InvariantCulture);
        _writer.WriteStartElement(new Row(), new[] { new OpenXmlAttribute("r", string.Empty, rowNumber) });

        for (var column = 1; column <= LastColumn; column++)
        {
            var cell = new Cell
            {
                CellReference = ColumnLetters[column - 1] + rowNumber,
                StyleIndex = _styles.Resolve(_columnStyles[column], lookOf(column)),
            };

            switch (values[column])
            {
                case string text when text.Length > 0:
                    cell.DataType = CellValues.InlineString;
                    cell.InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                    break;
                case int number:
                    cell.CellValue = new CellValue(number);
                    break;
                case double serial:
                    cell.CellValue = new CellValue(serial);
                    break;
            }

            _writer.WriteElement(cell);
        }

        _writer.WriteEndElement();
    }

    /// <summary>Cột I / K: a real date-time (Excel serial in Vietnam time), not text — sortable.</summary>
    private static double ToExcelTime(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, VietnamTime.TimeZone).DateTime.ToOADate();
}
