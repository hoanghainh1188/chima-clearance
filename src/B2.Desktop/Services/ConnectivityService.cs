using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using B2.Desktop.Services.Api;

namespace B2.Desktop.Services;

/// <summary>
/// Owns SignalR hub connection + lightweight HTTP reachability for FR-028.
/// Fires <see cref="SuggestedListRefresh"/> when polling fallback is active (≈3 s).
/// </summary>
public sealed class ConnectivityService : IDisposable, IAsyncDisposable
{
    private readonly IAuthTokenStore _auth;
    private readonly DesktopRuntimeOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ConnectivityService> _logger;
    private readonly object _gate = new();
    private HubConnection? _hub;
    private PeriodicTimer? _pollTimer;
    private CancellationTokenSource? _lifecycleCts;
    private bool _started;
    private bool _disposed;
    private ConnectivityMode _mode = ConnectivityMode.Offline;

    public ConnectivityService(
        DesktopRuntimeOptions options,
        IAuthTokenStore auth,
        IServiceScopeFactory scopeFactory,
        ILogger<ConnectivityService> logger)
    {
        _options = options;
        _auth = auth;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public event EventHandler? ModeChanged;

    public event Action? SuggestedListRefresh;

    public event Action<Guid>? DeclarationLedgerChanged;

    public event Action<DateOnly, string>? DailyHeaderOfficerChanged;

    public ConnectivityMode Mode
    {
        get
        {
            lock (_gate)
            {
                return _mode;
            }
        }
        private set
        {
            ConnectivityMode previous;
            bool changed;
            lock (_gate)
            {
                previous = _mode;
                changed = _mode != value;
                _mode = value;
            }

            if (changed)
            {
                _logger.LogInformation(
                    "FR-028 connectivity state changed: {Previous} -> {Current}",
                    previous,
                    value);
                ModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _lifecycleCts = new CancellationTokenSource();
        }

        _ = RunAsync(_lifecycleCts.Token);
    }

    public async Task StopAsync()
    {
        PeriodicTimer? timer;
        HubConnection? hub;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            timer = _pollTimer;
            _pollTimer = null;
            hub = _hub;
            _hub = null;
            cts = _lifecycleCts;
            _lifecycleCts = null;
            _started = false;
        }

        cts?.Cancel();
        timer?.Dispose();

        if (hub is not null)
        {
            await hub.DisposeAsync().ConfigureAwait(false);
        }

        cts?.Dispose();
        Mode = ConnectivityMode.Offline;
    }

    private async Task RunAsync(CancellationToken appStopping)
    {
        await UpdateModeFromHealthAsync(appStopping).ConfigureAwait(false);
        await EnsureHubAsync(appStopping).ConfigureAwait(false);
    }

    private async Task UpdateModeFromHealthAsync(CancellationToken ct)
    {
        var ok = await TryPingHealthAsync(ct).ConfigureAwait(false);
        if (!ok)
        {
            Mode = ConnectivityMode.Offline;
        }
    }

    private async Task EnsureHubAsync(CancellationToken ct)
    {
        HubConnection? hub = null;
        lock (_gate)
        {
            if (_hub is not null)
            {
                return;
            }

            hub = new HubConnectionBuilder()
                .WithUrl(
                    _options.HubUri,
                    options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(_auth.AccessToken);
                    })
                .WithAutomaticReconnect()
                .Build();

            hub.On<LedgerChangedPayload>(
                "LedgerChanged",
                payload =>
                {
                    MainThread.BeginInvokeOnMainThread(() => DeclarationLedgerChanged?.Invoke(payload.DeclarationId));
                });

            hub.On<DailyHeaderOfficerChangedPayload>(
                "DailyHeaderOfficerChanged",
                payload =>
                {
                    if (DateOnly.TryParse(payload.Date, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        MainThread.BeginInvokeOnMainThread(() => DailyHeaderOfficerChanged?.Invoke(date, payload.OfficerName));
                    }
                });

            hub.Reconnecting += _ =>
            {
                Mode = ConnectivityMode.PollingFallback;
                return Task.CompletedTask;
            };

            hub.Reconnected += _ =>
            {
                StopPollingTimer();
                Mode = ConnectivityMode.Online;
                return Task.CompletedTask;
            };

            hub.Closed += async _ =>
            {
                Mode = ConnectivityMode.PollingFallback;
                await StartPollingIfNeededAsync(ct).ConfigureAwait(false);
            };

            _hub = hub;
        }

        if (hub is null)
        {
            return;
        }

        try
        {
            Mode = ConnectivityMode.PollingFallback;
            await hub.StartAsync(ct).ConfigureAwait(false);
            StopPollingTimer();
            Mode = ConnectivityMode.Online;
        }
        catch
        {
            await StartPollingIfNeededAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task StartPollingIfNeededAsync(CancellationToken ct)
    {
        var httpOk = await TryPingHealthAsync(ct).ConfigureAwait(false);
        Mode = httpOk ? ConnectivityMode.PollingFallback : ConnectivityMode.Offline;
        if (!httpOk)
        {
            return;
        }

        PeriodicTimer? timer;
        lock (_gate)
        {
            if (_pollTimer is not null)
            {
                return;
            }

            _pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(3));
            timer = _pollTimer;
        }

        _ = PollLoopAsync(timer!, ct);
    }

    private async Task PollLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                HubConnection? hub;
                lock (_gate)
                {
                    hub = _hub;
                }

                if (hub?.State == HubConnectionState.Connected)
                {
                    StopPollingTimer();
                    Mode = ConnectivityMode.Online;
                    break;
                }

                var ok = await TryPingHealthAsync(ct).ConfigureAwait(false);
                Mode = ok ? ConnectivityMode.PollingFallback : ConnectivityMode.Offline;
                if (ok)
                {
                    // Bug #137: When server HTTP is reachable but hub was Closed/Disconnected,
                    // attempt to restart the SignalR connection to restore real-time events.
                    if (hub is not null && hub.State == HubConnectionState.Disconnected)
                    {
                        try
                        {
                            await hub.StartAsync(ct).ConfigureAwait(false);
                            if (hub.State == HubConnectionState.Connected)
                            {
                                StopPollingTimer();
                                Mode = ConnectivityMode.Online;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "SignalR reconnect attempt failed during polling fallback");
                        }
                    }

                    MainThread.BeginInvokeOnMainThread(() => SuggestedListRefresh?.Invoke());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    private void StopPollingTimer()
    {
        PeriodicTimer? timer;
        lock (_gate)
        {
            timer = _pollTimer;
            _pollTimer = null;
        }

        timer?.Dispose();
    }

    private async Task<bool> TryPingHealthAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<IB2Api>();
            var body = await api.GetHealthAsync(ct).ConfigureAwait(false);
            return string.Equals(body.Status, "OK", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!BeginDispose(out var hub, out var cts))
        {
            return;
        }

        // HubConnection only exposes async disposal; block on it so the synchronous
        // IDisposable path does not leak the underlying TCP connection. The carry-over
        // (T-S4-CO-09) explicitly sanctions a synchronous block here.
        hub?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        cts?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (!BeginDispose(out var hub, out var cts))
        {
            return;
        }

        if (hub is not null)
        {
            await hub.DisposeAsync().ConfigureAwait(false);
        }

        cts?.Dispose();
    }

    /// <summary>
    /// Flips the disposed flag exactly once, detaches the hub + lifecycle token, and
    /// stops the poll timer. Returns false on any subsequent call so both Dispose
    /// paths share teardown without double-disposing.
    /// </summary>
    private bool BeginDispose(out HubConnection? hub, out CancellationTokenSource? cts)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                hub = null;
                cts = null;
                return false;
            }

            _disposed = true;
            hub = _hub;
            _hub = null;
            cts = _lifecycleCts;
            _lifecycleCts = null;
        }

        StopPollingTimer();
        cts?.Cancel();
        return true;
    }
}
