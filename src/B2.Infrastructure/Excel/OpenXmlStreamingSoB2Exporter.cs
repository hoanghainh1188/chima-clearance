using System.Globalization;
using System.IO.Compression;
using System.Text;
using B2.Application.Abstractions;
using B2.Application.Export;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace B2.Infrastructure.Excel;

/// <summary>
/// FR-021 / #167 — writes the Sổ B2 layout of <see cref="ClosedXmlSoB2Exporter"/> (same template,
/// columns, colours, borders and duplicate rule) with a forward-only <see cref="OpenXmlWriter"/>,
/// so a full calendar year is streamed to disk instead of built as a ClosedXML DOM.
/// The output zip is written in <see cref="ZipArchiveMode.Create"/>, which also streams; every
/// template part is copied as-is except the first worksheet (rewritten row by row) and
/// <c>styles.xml</c> (the template's styles plus the formats the rows need).
/// </summary>
public sealed class OpenXmlStreamingSoB2Exporter : ISoB2StreamingExporter
{
    private const uint HeaderTitleRow = 1;
    private const int LastColumn = 15; // A..O
    private const int DeclarationNumberColumn = 3; // C

    /// <summary>Cột P, row 1: the legacy "Tìm xe" input box; the template ships a sample value.</summary>
    private const int SearchBoxColumn = 16;

    private const string RelationshipsNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly string[] ColumnLetters =
        Enumerable.Range(0, SearchBoxColumn).Select(i => ((char)('A' + i)).ToString()).ToArray();

    private readonly SoB2Template _template;

    /// <param name="dataDirectory">ProgramData root; see <see cref="SoB2Template"/> for the override.</param>
    public OpenXmlStreamingSoB2Exporter(string dataDirectory) => _template = new SoB2Template(dataDirectory);

    public async Task ExportAsync(
        IAsyncEnumerable<SoB2DayBlock> days,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(destination);

        // The template is a few dozen KB; holding it is fine — the output is what must stream.
        var templateBytes = await ReadTemplateAsync(cancellationToken);
        var parts = TemplateParts.Load(templateBytes);
        var styles = new SoB2StyleSheet(parts.Stylesheet);

        using var templateZip = new ZipArchive(new MemoryStream(templateBytes, writable: false), ZipArchiveMode.Read);
        using var output = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        // styles.xml is written last: it only knows every cell format once all rows are out.
        foreach (var entry in templateZip.Entries.Where(e => !parts.IsStyles(e.FullName)))
        {
            var target = output.CreateEntry(entry.FullName);
            await using var targetStream = target.Open();
            if (parts.IsSheet(entry.FullName))
            {
                await WriteSheetAsync(parts.Worksheet, styles, days, targetStream, cancellationToken);
            }
            else
            {
                await using var source = entry.Open();
                await source.CopyToAsync(targetStream, cancellationToken);
            }
        }

        var stylesEntry = output.CreateEntry(parts.StylesEntryName);
        await using var stylesStream = stylesEntry.Open();
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + styles.ToXml();
        await stylesStream.WriteAsync(new UTF8Encoding(false).GetBytes(xml), cancellationToken);
    }

    private static async Task WriteSheetAsync(
        Worksheet template,
        SoB2StyleSheet styles,
        IAsyncEnumerable<SoB2DayBlock> days,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var columnStyles = ReadColumnStyles(template);
        using var writer = OpenXmlWriter.Create(destination);
        writer.WriteStartDocument(true);
        writer.WriteStartElement(
            new Worksheet(),
            Array.Empty<OpenXmlAttribute>(),
            new[] { new KeyValuePair<string, string>("r", RelationshipsNamespace) });

        var afterSheetData = false;
        var conditionalFormatWritten = false;
        var lastRow = HeaderTitleRow;
        foreach (var child in template.ChildElements)
        {
            switch (child)
            {
                case Dimension:
                case ConditionalFormatting:
                    // Dimension changes with the data; the template's duplicate rules covered
                    // its sample rows and are replaced by one rule over every exported row.
                    continue;
                case SheetData sheetData:
                    writer.WriteStartElement(new SheetData());
                    WriteTitleRow(writer, sheetData);
                    var rows = new SheetRowWriter(writer, styles, columnStyles);
                    await foreach (var day in days.WithCancellation(cancellationToken))
                    {
                        rows.WriteDay(day);
                    }

                    lastRow = rows.LastRow;
                    writer.WriteEndElement();
                    afterSheetData = true;
                    continue;
            }

            if (afterSheetData && !conditionalFormatWritten && !PrecedesConditionalFormatting(child))
            {
                WriteDuplicateRule(writer, styles, lastRow);
                conditionalFormatWritten = true;
            }

            writer.WriteElement(SanitizeTemplateElement(child.CloneNode(true)));
        }

        if (!conditionalFormatWritten)
        {
            WriteDuplicateRule(writer, styles, lastRow);
        }

        writer.WriteEndElement();
        writer.Close();
    }

    /// <summary>Row 1 of the template (column titles) minus the sample "Tìm xe" value in P1.</summary>
    private static void WriteTitleRow(OpenXmlWriter writer, SheetData templateSheetData)
    {
        var title = templateSheetData.Elements<Row>().FirstOrDefault(r => (r.RowIndex?.Value ?? HeaderTitleRow) == HeaderTitleRow);
        if (title is null)
        {
            return;
        }

        var row = (Row)SanitizeTemplateElement(title.CloneNode(true));
        var searchBox = row.Elements<Cell>().FirstOrDefault(c =>
            c.CellReference?.Value == ColumnLetters[SearchBoxColumn - 1] + HeaderTitleRow.ToString(CultureInfo.InvariantCulture));
        if (searchBox is not null)
        {
            searchBox.CellValue = null;
            searchBox.DataType = null;
        }

        writer.WriteElement(row);
    }

    /// <summary>
    /// Business rules cột C: a duplicated declaration number is flagged red — one rule over
    /// every exported row, in the template's colours (as <see cref="ClosedXmlSoB2Exporter"/>).
    /// </summary>
    private static void WriteDuplicateRule(OpenXmlWriter writer, SoB2StyleSheet styles, uint lastRow)
    {
        if (lastRow <= HeaderTitleRow)
        {
            return;
        }

        var column = ColumnLetters[DeclarationNumberColumn - 1];
        writer.WriteElement(new ConditionalFormatting(new ConditionalFormattingRule
        {
            Type = ConditionalFormatValues.DuplicateValues,
            FormatId = styles.DuplicateFormatId,
            Priority = 1,
        })
        {
            SequenceOfReferences = new ListValue<StringValue>
            {
                InnerText = string.Create(CultureInfo.InvariantCulture, $"{column}{HeaderTitleRow + 1}:{column}{lastRow}"),
            },
        });
    }

    /// <summary>
    /// Schema order (CT_Worksheet): these elements sit between sheetData and
    /// conditionalFormatting; anything else comes after the duplicate rule.
    /// </summary>
    private static bool PrecedesConditionalFormatting(OpenXmlElement element) => element
        is SheetCalculationProperties or SheetProtection or ProtectedRanges or Scenarios
        or AutoFilter or SortState or DataConsolidate or CustomSheetViews or MergeCells
        or PhoneticProperties;

    /// <summary>
    /// The output root does not carry <c>mc:Ignorable="x14ac"</c>, so drop the x14ac row-height
    /// hints copied from the template rather than emit an attribute Excel would not ignore; and
    /// drop the template's schema-invalid <c>verticalDpi="0"</c> (ClosedXML drops it too).
    /// </summary>
    private static OpenXmlElement SanitizeTemplateElement(OpenXmlElement element)
    {
        switch (element)
        {
            case Row row:
                row.DyDescent = null;
                break;
            case SheetFormatProperties format:
                format.DyDescent = null;
                break;
            case PageSetup pageSetup:
                if (pageSetup.VerticalDpi?.Value == 0)
                {
                    pageSetup.VerticalDpi = null;
                }

                if (pageSetup.HorizontalDpi?.Value == 0)
                {
                    pageSetup.HorizontalDpi = null;
                }

                break;
        }

        return element;
    }

    private static uint[] ReadColumnStyles(Worksheet template)
    {
        var styles = new uint[LastColumn + 1];
        foreach (var column in template.GetFirstChild<Columns>()?.Elements<Column>() ?? Enumerable.Empty<Column>())
        {
            var style = column.Style?.Value ?? 0;
            var min = column.Min?.Value ?? 1;
            var max = Math.Min(column.Max?.Value ?? min, (uint)LastColumn);
            for (var c = min; c <= max; c++)
            {
                styles[c] = style;
            }
        }

        return styles;
    }

    private async Task<byte[]> ReadTemplateAsync(CancellationToken cancellationToken)
    {
        await using var template = _template.Open();
        using var buffer = new MemoryStream();
        await template.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    /// <summary>The template's first worksheet and stylesheet, and where they live in the zip.</summary>
    private sealed record TemplateParts(
        Worksheet Worksheet,
        Stylesheet Stylesheet,
        string SheetEntryName,
        string StylesEntryName)
    {
        public static TemplateParts Load(byte[] templateBytes)
        {
            using var document = SpreadsheetDocument.Open(new MemoryStream(templateBytes, writable: false), false);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Sổ B2 template has no workbook part.");
            var sheet = workbookPart.Workbook.Sheets?.Elements<Sheet>().FirstOrDefault()
                ?? throw new InvalidOperationException("Sổ B2 template has no worksheet.");
            var sheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id?.Value
                ?? throw new InvalidOperationException("Sổ B2 template sheet has no relationship id."));
            var stylesPart = workbookPart.WorkbookStylesPart
                ?? throw new InvalidOperationException("Sổ B2 template has no styles part.");

            return new TemplateParts(
                (Worksheet)sheetPart.Worksheet.CloneNode(true),
                (Stylesheet)(stylesPart.Stylesheet ?? new Stylesheet()).CloneNode(true),
                EntryName(sheetPart),
                EntryName(stylesPart));
        }

        public bool IsSheet(string entryName) => string.Equals(entryName, SheetEntryName, StringComparison.OrdinalIgnoreCase);

        public bool IsStyles(string entryName) => string.Equals(entryName, StylesEntryName, StringComparison.OrdinalIgnoreCase);

        private static string EntryName(OpenXmlPart part) => part.Uri.OriginalString.TrimStart('/');
    }
}
