namespace B2.Desktop.Services;

/// <summary>LAN API base (must match Refit <see cref="Api.IB2Api"/> host).</summary>
public sealed class DesktopRuntimeOptions
{
    public DesktopRuntimeOptions(Uri apiBaseUri)
    {
        ApiBaseUri = apiBaseUri;
    }

    public Uri ApiBaseUri { get; }

    public Uri HubUri => new(ApiBaseUri, "hubs/events");
}
