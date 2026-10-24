namespace B2.Infrastructure.Excel;

/// <summary>
/// Locates the Sổ B2 template (<c>Mẫu sổ B2.xlsx</c>) shared by the interactive F-13 export and
/// the FR-021 streaming archive: the on-disk copy under <c>{dataDirectory}/templates/</c> wins
/// over the embedded resource, so the unit can re-skin both without a redeploy.
/// </summary>
internal sealed class SoB2Template
{
    private readonly string? _onDiskPath;

    public SoB2Template(string dataDirectory)
    {
        var candidate = Path.Combine(dataDirectory, "templates", "Mẫu sổ B2.xlsx");
        _onDiskPath = File.Exists(candidate) ? candidate : null;
    }

    public Stream Open()
    {
        if (_onDiskPath is not null)
        {
            return File.OpenRead(_onDiskPath);
        }

        var assembly = typeof(SoB2Template).Assembly;
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("MauSoB2.xlsx", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Embedded Sổ B2 template 'MauSoB2.xlsx' was not found.");

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded Sổ B2 template stream could not be opened.");
    }
}
