namespace B2.Desktop.Services;

/// <summary>
/// F-17 latency guard: two consecutive suggestion round-trips over the 100 ms budget hide the
/// suggestion zone. It used to stay hidden for the whole session, so one Wi-Fi hiccup cost the
/// operator suggestions until the app was restarted (#160); now it comes back after a cooldown.
/// Pure BCL so the test project can link it.
/// </summary>
public sealed class SuggestionLatencyGuard
{
    public static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    private const int OverrunsToSuspend = 2;

    private readonly TimeProvider _time;
    private int _consecutiveOverruns;
    private DateTimeOffset? _suspendedUntil;

    public SuggestionLatencyGuard(TimeProvider time)
    {
        _time = time;
    }

    /// <summary>False while suspended; the first call after the cooldown re-arms the guard.</summary>
    public bool CanQuery()
    {
        if (_suspendedUntil is not { } until)
        {
            return true;
        }

        if (_time.GetUtcNow() < until)
        {
            return false;
        }

        _suspendedUntil = null;
        _consecutiveOverruns = 0;
        return true;
    }

    /// <summary>
    /// Records one round-trip. Returns <c>true</c> when this overrun suspends suggestions — the
    /// caller then drops the (late) result instead of showing it.
    /// </summary>
    public bool RecordRoundTrip(TimeSpan elapsed)
    {
        if (elapsed <= Budget)
        {
            _consecutiveOverruns = 0;
            return false;
        }

        _consecutiveOverruns++;
        if (_consecutiveOverruns < OverrunsToSuspend)
        {
            return false;
        }

        _suspendedUntil = _time.GetUtcNow() + Cooldown;
        return true;
    }
}
