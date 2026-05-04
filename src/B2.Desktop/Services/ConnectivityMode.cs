namespace B2.Desktop.Services;

/// <summary>FR-028 tri-state connectivity for the desktop status bar.</summary>
public enum ConnectivityMode
{
    Online,
    PollingFallback,
    Offline,
}
