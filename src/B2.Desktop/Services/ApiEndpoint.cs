namespace B2.Desktop.Services;

/// <summary>
/// The resolved server address for this client, plus where it came from. <paramref name="Warning"/>
/// is non-null when a configured value was rejected and the fallback was used — the caller logs it
/// so a mistyped LAN address at the exit gate is diagnosable without a debugger.
/// </summary>
public sealed record ApiEndpoint(Uri BaseUri, ApiEndpointSource Source, string? Warning);
