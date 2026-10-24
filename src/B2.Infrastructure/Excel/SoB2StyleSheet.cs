using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Spreadsheet;

namespace B2.Infrastructure.Excel;

/// <summary>
/// A copy of the template's <c>styles.xml</c> that grows one <c>cellXfs</c> entry per
/// (template column style, <see cref="SoB2CellLook"/>) pair on first use. The colours and the
/// timestamp format are the ones <see cref="ClosedXmlSoB2Exporter"/> uses, so the streamed
/// archive looks like the interactive export. The stylesheet is small; only it is kept as a DOM.
/// </summary>
internal sealed class SoB2StyleSheet
{
    private const string UnexitedRed = "FFFF0000";
    private const string AllExitedGreen = "FFC6EFCE";
    private const string DayHeaderBlue = "FFD9E1F2";
    private const string DuplicateText = "FF9C0006";
    private const string DuplicateFill = "FFFFC7CE";

    /// <summary>Display form from business rules cột I/K: "26/2/2026 13:16".</summary>
    private const string TimestampFormat = "d/M/yyyy H:mm";

    private readonly Stylesheet _stylesheet;
    private readonly Dictionary<(uint BaseXf, SoB2CellLook Look), uint> _cellFormats = new();
    private readonly Dictionary<(uint BaseFont, SoB2CellLook FontLook), uint> _fonts = new();
    private readonly Dictionary<string, uint> _fills = new(StringComparer.Ordinal);
    private uint? _thinBorderId;
    private uint? _timestampFormatId;

    public SoB2StyleSheet(Stylesheet templateStylesheet)
    {
        _stylesheet = (Stylesheet)templateStylesheet.CloneNode(true);
        DuplicateFormatId = AddDuplicateDifferentialFormat();
    }

    /// <summary><c>dxfId</c> of the "light red fill with dark red text" duplicate rule (cột C).</summary>
    public uint DuplicateFormatId { get; }

    public uint Resolve(uint baseXf, SoB2CellLook look)
    {
        if (look == SoB2CellLook.None)
        {
            return baseXf;
        }

        if (_cellFormats.TryGetValue((baseXf, look), out var existing))
        {
            return existing;
        }

        var cellXfs = _stylesheet.CellFormats ??= new CellFormats(new CellFormat());
        var template = cellXfs.Elements<CellFormat>().ElementAtOrDefault((int)baseXf) ?? new CellFormat();
        var format = (CellFormat)template.CloneNode(true);

        const SoB2CellLook fontLook = SoB2CellLook.Bold | SoB2CellLook.RedText | SoB2CellLook.Strikethrough;
        if ((look & fontLook) != 0)
        {
            format.FontId = ResolveFont(format.FontId?.Value ?? 0, look & fontLook);
            format.ApplyFont = true;
        }

        if (look.HasFlag(SoB2CellLook.DayHeaderFill) || look.HasFlag(SoB2CellLook.AllExitedFill))
        {
            format.FillId = ResolveFill(look.HasFlag(SoB2CellLook.DayHeaderFill) ? DayHeaderBlue : AllExitedGreen);
            format.ApplyFill = true;
        }

        if (look.HasFlag(SoB2CellLook.ThinBorder))
        {
            format.BorderId = _thinBorderId ??= AddThinBorder();
            format.ApplyBorder = true;
        }

        if (look.HasFlag(SoB2CellLook.Timestamp))
        {
            format.NumberFormatId = _timestampFormatId ??= AddTimestampFormat();
            format.ApplyNumberFormat = true;
        }

        if (look.HasFlag(SoB2CellLook.WrapText))
        {
            format.Alignment ??= new Alignment();
            format.Alignment.WrapText = true;
            format.ApplyAlignment = true;
        }

        cellXfs.AppendChild(format);
        cellXfs.Count = (uint)cellXfs.ChildElements.Count;
        var index = cellXfs.Count.Value - 1;
        _cellFormats[(baseXf, look)] = index;
        return index;
    }

    /// <summary>Serialized <c>styles.xml</c>, including every format resolved so far.</summary>
    public string ToXml() => _stylesheet.OuterXml;

    private uint ResolveFont(uint baseFont, SoB2CellLook fontLook)
    {
        if (_fonts.TryGetValue((baseFont, fontLook), out var existing))
        {
            return existing;
        }

        var fonts = _stylesheet.Fonts ??= new Fonts(new Font());
        var template = fonts.Elements<Font>().ElementAtOrDefault((int)baseFont) ?? new Font();
        var font = (Font)template.CloneNode(true);
        if (fontLook.HasFlag(SoB2CellLook.Bold))
        {
            font.Bold = new Bold();
        }

        if (fontLook.HasFlag(SoB2CellLook.RedText))
        {
            font.Color = new Color { Rgb = UnexitedRed };
        }

        if (fontLook.HasFlag(SoB2CellLook.Strikethrough))
        {
            font.Strike = new Strike();
        }

        fonts.AppendChild(font);
        fonts.Count = (uint)fonts.ChildElements.Count;
        var index = fonts.Count.Value - 1;
        _fonts[(baseFont, fontLook)] = index;
        return index;
    }

    private uint ResolveFill(string argb)
    {
        if (_fills.TryGetValue(argb, out var existing))
        {
            return existing;
        }

        var fills = _stylesheet.Fills ??= new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
        fills.AppendChild(new Fill(new PatternFill(
            new ForegroundColor { Rgb = argb },
            new BackgroundColor { Indexed = 64U })
        { PatternType = PatternValues.Solid }));
        fills.Count = (uint)fills.ChildElements.Count;
        var index = fills.Count.Value - 1;
        _fills[argb] = index;
        return index;
    }

    private uint AddThinBorder()
    {
        var borders = _stylesheet.Borders ??= new Borders(new Border());
        borders.AppendChild(new Border(
            new LeftBorder { Style = BorderStyleValues.Thin },
            new RightBorder { Style = BorderStyleValues.Thin },
            new TopBorder { Style = BorderStyleValues.Thin },
            new BottomBorder { Style = BorderStyleValues.Thin },
            new DiagonalBorder()));
        borders.Count = (uint)borders.ChildElements.Count;
        return borders.Count.Value - 1;
    }

    private uint AddTimestampFormat()
    {
        var formats = _stylesheet.NumberingFormats ??= new NumberingFormats();
        var match = formats.Elements<NumberingFormat>()
            .FirstOrDefault(f => f.FormatCode?.Value == TimestampFormat);
        if (match?.NumberFormatId?.Value is { } existingId)
        {
            return existingId;
        }

        // Custom number formats start at 164; built-ins occupy 0–163.
        var id = Math.Max(164U, formats.Elements<NumberingFormat>()
            .Select(f => (f.NumberFormatId?.Value ?? 0) + 1)
            .DefaultIfEmpty(0U)
            .Max());
        formats.AppendChild(new NumberingFormat { NumberFormatId = id, FormatCode = TimestampFormat });
        formats.Count = (uint)formats.ChildElements.Count;
        return id;
    }

    private uint AddDuplicateDifferentialFormat()
    {
        var dxfs = _stylesheet.DifferentialFormats ??= new DifferentialFormats();
        dxfs.AppendChild(new DifferentialFormat(
            new Font(new Color { Rgb = DuplicateText }),
            new Fill(new PatternFill(new BackgroundColor { Rgb = DuplicateFill }))));
        dxfs.Count = (uint)dxfs.ChildElements.Count;
        return dxfs.Count.Value - 1;
    }
}
