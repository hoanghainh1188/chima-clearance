namespace B2.Desktop.Services;

/// <summary>Where <see cref="ApiEndpointResolver"/> read the server address from.</summary>
public enum ApiEndpointSource
{
    /// <summary>No override found — the loopback default was used (server on this PC).</summary>
    Default = 0,

    /// <summary>From <c>b2-client.json</c> next to the executable (exit-gate PC).</summary>
    ConfigFile = 1,

    /// <summary>From the <c>B2_SERVER_URL</c> environment variable (dev / troubleshooting).</summary>
    Environment = 2,
}
