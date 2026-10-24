namespace B2.Desktop.Services;

/// <summary>What one FR-028 polling tick should do; see <see cref="ConnectivityPolicy.Decide"/>.</summary>
public readonly record struct ConnectivityTick(
    ConnectivityMode Mode,
    bool StopPolling,
    bool TryRestartHub,
    bool SuggestRefresh);

/// <summary>
/// FR-028 tri-state decision for each ≈3 s tick while SignalR is not connected. Kept free of
/// MAUI / SignalR types so it is unit-tested in B2.Desktop.Tests.
/// <para>
/// Bug #137: the tick keeps running while the server is unreachable (🔴). Previously the
/// timer was only created when the health check passed, so a client that lost the LAN —
/// the usual reason the hub closes — or started while the server was down stayed red
/// forever, with nothing left to retry.
/// </para>
/// </summary>
public static class ConnectivityPolicy
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    /// <summary>Upper bound on one hub restart attempt, so a hung handshake cannot stall polling.</summary>
    public static readonly TimeSpan HubStartTimeout = TimeSpan.FromSeconds(10);

    public static ConnectivityTick Decide(bool hubConnected, bool hubDisconnected, bool healthOk)
    {
        if (hubConnected)
        {
            return new ConnectivityTick(ConnectivityMode.Online, StopPolling: true, TryRestartHub: false, SuggestRefresh: false);
        }

        if (!healthOk)
        {
            // Stay on the timer: the next tick probes again.
            return new ConnectivityTick(ConnectivityMode.Offline, StopPolling: false, TryRestartHub: false, SuggestRefresh: false);
        }

        // HTTP is back. Restart the hub only when it has given up (Disconnected); while it is
        // Reconnecting/Connecting its own retry loop owns the connection.
        return new ConnectivityTick(ConnectivityMode.PollingFallback, StopPolling: false, TryRestartHub: hubDisconnected, SuggestRefresh: true);
    }
}
