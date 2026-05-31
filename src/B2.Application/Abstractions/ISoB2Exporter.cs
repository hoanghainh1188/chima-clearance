using B2.Application.Export;

namespace B2.Application.Abstractions;

/// <summary>
/// Renders assembled <see cref="SoB2ExportData"/> into a Sổ B2 .xlsx byte array.
/// Implemented by the Infrastructure ClosedXML exporter (template-based, FR-015).
/// </summary>
public interface ISoB2Exporter
{
    byte[] Export(SoB2ExportData data);
}
