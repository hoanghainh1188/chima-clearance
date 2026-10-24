namespace B2.Infrastructure.IO;

/// <summary>
/// Crash-safe file replacement: write a sibling temp file, flush it to disk, then rename it
/// over the target. A power cut mid-write leaves either the old file or the new one — never a
/// truncated secret (#169, #148).
/// </summary>
internal static class AtomicFile
{
    public static void WriteAllBytes(string path, byte[] bytes)
    {
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    public static void WriteAllText(string path, string contents) =>
        WriteAllBytes(path, System.Text.Encoding.UTF8.GetBytes(contents));
}
