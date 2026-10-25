namespace B2.Desktop.Services;

/// <summary>
/// FR-018 / F-01 "Auto-logout on idle": the session ends after 30 minutes with no keyboard or
/// pointer input. The access token lives 8 h and the refresh token rolls for 7 days, so without
/// this an unattended gate PC stayed signed in indefinitely (security checklist § 10).
/// Background polling and SignalR traffic are not activity — only the operator's input is.
/// Pure BCL so the test project can link it.
/// </summary>
public sealed class IdleSessionPolicy
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    /// <summary>How often the shell checks; the logout lands at most this long after the deadline.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _time;
    private DateTimeOffset _lastActivity;

    public IdleSessionPolicy(TimeProvider time)
    {
        _time = time;
        _lastActivity = time.GetUtcNow();
    }

    public void RecordActivity() => _lastActivity = _time.GetUtcNow();

    public bool IsExpired() => _time.GetUtcNow() - _lastActivity >= Timeout;
}
