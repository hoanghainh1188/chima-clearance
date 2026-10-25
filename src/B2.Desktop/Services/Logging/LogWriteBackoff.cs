namespace B2.Desktop.Services.Logging;

/// <summary>
/// When the desktop log file cannot be written — usually a scanner or backup tool holding the
/// file for a moment — skip writes for a short, doubling interval instead of switching file
/// logging off for the rest of the session (#171). Lines that arrive during the pause are
/// dropped; a successful write resets the interval. Pure BCL so the test project can link it.
/// </summary>
public sealed class LogWriteBackoff
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time;
    private TimeSpan _nextDelay = InitialDelay;
    private DateTimeOffset? _pausedUntil;

    public LogWriteBackoff(TimeProvider time)
    {
        _time = time;
    }

    public bool CanWrite() => _pausedUntil is not { } until || _time.GetUtcNow() >= until;

    public void RecordSuccess()
    {
        _pausedUntil = null;
        _nextDelay = InitialDelay;
    }

    public void RecordFailure()
    {
        _pausedUntil = _time.GetUtcNow() + _nextDelay;
        var doubled = _nextDelay + _nextDelay;
        _nextDelay = doubled < MaxDelay ? doubled : MaxDelay;
    }
}
