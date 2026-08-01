namespace B2.Desktop.Services;

/// <summary>LAN API base (must match Refit <see cref="Api.IB2Api"/> host).</summary>
public sealed class DesktopRuntimeOptions
{
    public DesktopRuntimeOptions(Uri apiBaseUri)
        : this(apiBaseUri, ApiEndpointSource.Default)
    {
    }

    public DesktopRuntimeOptions(Uri apiBaseUri, ApiEndpointSource apiBaseSource)
    {
        ApiBaseUri = apiBaseUri;
        ApiBaseSource = apiBaseSource;
    }

    public Uri ApiBaseUri { get; }

    /// <summary>Where <see cref="ApiBaseUri"/> came from — surfaced in diagnostics.</summary>
    public ApiEndpointSource ApiBaseSource { get; }

    public Uri HubUri => new(ApiBaseUri, "hubs/events");
}
