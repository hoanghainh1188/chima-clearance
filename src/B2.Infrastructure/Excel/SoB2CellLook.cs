namespace B2.Infrastructure.Excel;

/// <summary>
/// The formatting the Sổ B2 layout lays over a template column style — the same set of
/// properties <see cref="ClosedXmlSoB2Exporter"/> applies through ClosedXML, as flags so the
/// streaming archive writer can resolve each combination to one cached <c>cellXfs</c> index.
/// </summary>
[Flags]
internal enum SoB2CellLook
{
    None = 0,
    ThinBorder = 1 << 0,
    Bold = 1 << 1,
    RedText = 1 << 2,
    Strikethrough = 1 << 3,
    DayHeaderFill = 1 << 4,
    AllExitedFill = 1 << 5,
    Timestamp = 1 << 6,
    WrapText = 1 << 7,
}
