using System.Text;

namespace B2.Infrastructure.Backup;

/// <summary>Renders a Litestream YAML config (one DB, N file replicas) from the options.</summary>
public static class LitestreamConfigWriter
{
    public static string ToYaml(LitestreamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var sb = new StringBuilder();
        sb.AppendLine("dbs:");
        sb.AppendLine("  - path: " + options.DbPath);
        sb.AppendLine("    replicas:");
        foreach (var replica in options.Replicas)
        {
            sb.AppendLine("      - type: file");
            if (!string.IsNullOrWhiteSpace(replica.Name))
            {
                sb.AppendLine("        name: " + replica.Name);
            }

            sb.AppendLine("        path: " + replica.Path);
        }

        return sb.ToString();
    }
}
