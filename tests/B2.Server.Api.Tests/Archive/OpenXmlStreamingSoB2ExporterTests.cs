using System.Globalization;
using B2.Infrastructure.Excel;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using FluentAssertions;

namespace B2.Server.Api.Tests.Archive;

/// <summary>
/// #167 — the streamed archive must look like the interactive F-13 export (FR-015 layout):
/// same values and the same formatting, cell for cell, as <see cref="ClosedXmlSoB2Exporter"/>.
/// </summary>
public sealed class OpenXmlStreamingSoB2ExporterTests
{
    private const int LastColumn = 16; // A..P (P1 is the "Tìm xe" box)

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_match_the_closedxml_export_cell_for_cell()
    {
        var data = SoB2ExportSamples.Mixed();
        var dataDirectory = Path.GetTempPath();

        using var expected = new XLWorkbook(new MemoryStream(new ClosedXmlSoB2Exporter(dataDirectory).Export(data)));
        using var streamed = new MemoryStream();
        await new OpenXmlStreamingSoB2Exporter(dataDirectory)
            .ExportAsync(SoB2ExportSamples.Stream(data), streamed, CancellationToken.None);
        streamed.Position = 0;
        using var actual = new XLWorkbook(streamed);

        var expectedSheet = expected.Worksheet(1);
        var actualSheet = actual.Worksheet(1);
        var lastRow = expectedSheet.LastRowUsed()!.RowNumber();
        actualSheet.LastRowUsed()!.RowNumber().Should().Be(lastRow);
        actual.Worksheets.Count.Should().Be(expected.Worksheets.Count);

        for (var row = 1; row <= lastRow; row++)
        {
            for (var column = 1; column <= LastColumn; column++)
            {
                var e = expectedSheet.Cell(row, column);
                var a = actualSheet.Cell(row, column);
                var at = $"{e.Address}";

                // ClosedXML keeps Environment.NewLine (CR-LF on Windows) in cột C; the stream
                // writes Excel's LF. Same line break on screen.
                Normalize(a).Should().Be(Normalize(e), at);
                if (e.GetString().Length > 0)
                {
                    // ClosedXML stores "" as an empty text cell; the stream leaves it blank.
                    a.DataType.Should().Be(e.DataType, at);
                }

                a.Style.Font.Bold.Should().Be(e.Style.Font.Bold, at);
                a.Style.Font.Strikethrough.Should().Be(e.Style.Font.Strikethrough, at);
                a.Style.Font.FontColor.Should().Be(e.Style.Font.FontColor, at);
                a.Style.Font.FontName.Should().Be(e.Style.Font.FontName, at);
                a.Style.Font.FontSize.Should().Be(e.Style.Font.FontSize, at);
                a.Style.Fill.BackgroundColor.Should().Be(e.Style.Fill.BackgroundColor, at);
                a.Style.Border.LeftBorder.Should().Be(e.Style.Border.LeftBorder, at);
                a.Style.Border.TopBorder.Should().Be(e.Style.Border.TopBorder, at);
                a.Style.Border.RightBorder.Should().Be(e.Style.Border.RightBorder, at);
                a.Style.Border.BottomBorder.Should().Be(e.Style.Border.BottomBorder, at);
                a.Style.Alignment.WrapText.Should().Be(e.Style.Alignment.WrapText, at);
                a.Style.Alignment.Horizontal.Should().Be(e.Style.Alignment.Horizontal, at);
                a.Style.NumberFormat.Format.Should().Be(e.Style.NumberFormat.Format, at);
            }
        }

        for (var column = 1; column <= LastColumn; column++)
        {
            actualSheet.Column(column).Width.Should().Be(expectedSheet.Column(column).Width);
        }

        actualSheet.Row(1).Height.Should().Be(expectedSheet.Row(1).Height);
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_flag_duplicate_declaration_numbers_over_every_exported_row()
    {
        var data = SoB2ExportSamples.Mixed();
        using var streamed = new MemoryStream();
        await new OpenXmlStreamingSoB2Exporter(Path.GetTempPath())
            .ExportAsync(SoB2ExportSamples.Stream(data), streamed, CancellationToken.None);
        streamed.Position = 0;
        using var workbook = new XLWorkbook(streamed);
        var sheet = workbook.Worksheet(1);

        var rule = sheet.ConditionalFormats.Should().ContainSingle().Subject;
        rule.ConditionalFormatType.Should().Be(XLConditionalFormatType.IsDuplicate);
        rule.Range.RangeAddress.ToString().Should().Be($"C2:C{sheet.LastRowUsed()!.RowNumber()}");
        rule.Style.Font.FontColor.Should().Be(XLColor.FromArgb(0x9C, 0x00, 0x06));
    }

    [Fact]
    [Trait("Requirement", "FR-021")]
    public async Task Should_produce_a_schema_valid_workbook()
    {
        // Excel shows a "repair" prompt for a workbook that breaks the schema; the archive is
        // read years later, so it must open cleanly.
        using var streamed = new MemoryStream();
        await new OpenXmlStreamingSoB2Exporter(Path.GetTempPath())
            .ExportAsync(SoB2ExportSamples.Stream(SoB2ExportSamples.Mixed()), streamed, CancellationToken.None);
        streamed.Position = 0;
        using var document = SpreadsheetDocument.Open(streamed, false);

        var errors = new OpenXmlValidator(FileFormatVersions.Office2010).Validate(document)
            .Select(e => $"{e.Part?.Uri} {e.Path?.XPath}: {e.Description}");

        errors.Should().BeEmpty();
    }

    private static string Normalize(IXLCell cell) =>
        cell.Value.ToString(CultureInfo.InvariantCulture).Replace("\r\n", "\n", StringComparison.Ordinal);
}
