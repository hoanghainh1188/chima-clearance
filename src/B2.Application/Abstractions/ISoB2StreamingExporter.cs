using B2.Application.Export;

namespace B2.Application.Abstractions;

/// <summary>
/// Renders the Sổ B2 layout straight into <paramref name="destination"/> one day at a time,
/// without holding the whole workbook in memory. Used by the FR-021 annual archive, where a
/// full calendar year is too large for the in-memory <see cref="ISoB2Exporter"/> (#167).
/// </summary>
public interface ISoB2StreamingExporter
{
    Task ExportAsync(IAsyncEnumerable<SoB2DayBlock> days, Stream destination, CancellationToken cancellationToken);
}
