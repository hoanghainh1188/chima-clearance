using System.Text.Json;

namespace B2.Desktop.Services;

/// <summary>
/// Resolves which B2 server this desktop client talks to.
/// <para>
/// The gate runs two PCs (business rules § B): the entry-gate PC hosts the service and the
/// exit-gate PC is a client over the LAN. The address is therefore deployment data, not a
/// compile-time constant. Precedence, highest first:
/// </para>
/// <list type="number">
///   <item><description><c>B2_SERVER_URL</c> environment variable (dev / troubleshooting).</description></item>
///   <item><description><c>b2-client.json</c> next to the executable — <c>{"serverUrl": "..."}</c>,
///     written by <c>deploy/install.ps1 -ServerUrl</c>.</description></item>
///   <item><description>The loopback default (server on this same PC — the entry gate).</description></item>
/// </list>
/// <para>
/// Values are lenient by design: operators type <c>192.168.1.10</c>, not a URL. A bare host gets
/// <c>http://</c> and port 5000; a missing trailing slash is added (Refit drops the last segment
/// of a base address without one).
/// </para>
/// </summary>
public static class ApiEndpointResolver
{
    /// <summary>Server on this PC — the entry gate, and what a client install overrides.</summary>
    public const string DefaultBaseAddress = "http://127.0.0.1:5000/";

    /// <summary>Config file read from the executable's directory.</summary>
    public const string ConfigFileName = "b2-client.json";

    /// <summary>Property name inside <see cref="ConfigFileName"/> (matched case-insensitively).</summary>
    public const string ConfigPropertyName = "serverUrl";

    /// <summary>Environment variable checked before the config file.</summary>
    public const string EnvironmentVariableName = "B2_SERVER_URL";

    private const int DefaultPort = 5000;

    /// <summary>
    /// Resolves the endpoint. <paramref name="appDirectory"/> is where <see cref="ConfigFileName"/>
    /// is looked for (normally the executable's directory); <paramref name="environmentOverride"/> is
    /// the value of <see cref="EnvironmentVariableName"/>. Both may be null.
    /// </summary>
    public static ApiEndpoint Resolve(string? appDirectory, string? environmentOverride)
    {
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            var fromEnvironment = TryNormalize(environmentOverride);
            if (fromEnvironment is not null)
            {
                return new ApiEndpoint(fromEnvironment, ApiEndpointSource.Environment, null);
            }

            return Fallback(EnvironmentVariableName + " is not a valid server address: '"
                + environmentOverride.Trim() + "'.");
        }

        var (configured, readWarning) = ReadConfigFile(appDirectory);
        if (readWarning is not null)
        {
            return Fallback(readWarning);
        }

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var fromFile = TryNormalize(configured);
            if (fromFile is not null)
            {
                return new ApiEndpoint(fromFile, ApiEndpointSource.ConfigFile, null);
            }

            return Fallback(ConfigFileName + " has an invalid " + ConfigPropertyName + ": '"
                + configured.Trim() + "'.");
        }

        return new ApiEndpoint(new Uri(DefaultBaseAddress), ApiEndpointSource.Default, null);
    }

    private static ApiEndpoint Fallback(string warning) =>
        new(new Uri(DefaultBaseAddress),
            ApiEndpointSource.Default,
            warning + " Falling back to " + DefaultBaseAddress);

    /// <summary>
    /// Reads the configured address. A missing file is normal (entry gate). An unreadable or
    /// malformed file is NOT silently ignored — it means someone tried to configure this client
    /// and it did not take, which would otherwise surface only as "🔴 Mất kết nối".
    /// </summary>
    private static (string? Value, string? Warning) ReadConfigFile(string? appDirectory)
    {
        if (string.IsNullOrWhiteSpace(appDirectory))
        {
            return (null, null);
        }

        string path;
        try
        {
            path = Path.Combine(appDirectory, ConfigFileName);
        }
        catch (ArgumentException)
        {
            return (null, null);
        }

        if (!File.Exists(path))
        {
            return (null, null);
        }

        try
        {
            // Strip a UTF-8 BOM: Notepad writes one by default and System.Text.Json rejects it.
            var bytes = File.ReadAllBytes(path);
            ReadOnlyMemory<byte> json = bytes;
            if (json.Length >= 3
                && json.Span[0] == 0xEF && json.Span[1] == 0xBB && json.Span[2] == 0xBF)
            {
                json = json[3..];
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, ConfigFileName + " must contain a JSON object.");
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.NameEquals(ConfigPropertyName)
                    && !string.Equals(property.Name, ConfigPropertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value.ValueKind == JsonValueKind.String
                    ? (property.Value.GetString(), null)
                    : (null, ConfigFileName + " property " + ConfigPropertyName + " must be a string.");
            }

            return (null, null);
        }
        catch (JsonException)
        {
            return (null, ConfigFileName + " is not valid JSON.");
        }
        catch (IOException ex)
        {
            return (null, ConfigFileName + " could not be read: " + ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return (null, ConfigFileName + " could not be read: " + ex.Message);
        }
    }

    /// <summary>
    /// Accepts what an operator would actually type — <c>192.168.1.10</c>,
    /// <c>192.168.1.10:5000</c>, <c>http://gate-pc:5000</c> — and returns a base URI Refit can use,
    /// or null when the value cannot be understood.
    /// </summary>
    private static Uri? TryNormalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        var hasScheme = text.Contains("://", StringComparison.Ordinal);
        if (!hasScheme)
        {
            text = Uri.UriSchemeHttp + "://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed))
        {
            return null;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        if (string.IsNullOrEmpty(parsed.Host))
        {
            return null;
        }

        var port = HasExplicitPort(text) ? parsed.Port : DefaultPort;
        var path = parsed.AbsolutePath.EndsWith('/') ? parsed.AbsolutePath : parsed.AbsolutePath + "/";

        return new UriBuilder(parsed.Scheme, parsed.Host, port, path).Uri;
    }

    /// <summary>
    /// True when the authority carries an explicit <c>:port</c>. <see cref="Uri.IsDefaultPort"/>
    /// cannot answer this — it is also true for an explicit <c>:80</c> — and a bare host must land
    /// on 5000, not 80.
    /// </summary>
    private static bool HasExplicitPort(string textWithScheme)
    {
        var afterScheme = textWithScheme.IndexOf("://", StringComparison.Ordinal) + 3;
        var authority = textWithScheme[afterScheme..];

        var end = authority.IndexOfAny(['/', '?', '#']);
        if (end >= 0)
        {
            authority = authority[..end];
        }

        // IPv6 literals ("[::1]:5000") carry colons inside the brackets.
        var closingBracket = authority.LastIndexOf(']');
        var searchFrom = closingBracket >= 0 ? closingBracket : 0;

        return authority.IndexOf(':', searchFrom) >= 0;
    }
}
