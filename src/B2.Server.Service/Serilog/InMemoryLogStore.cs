using Serilog.Core;
using Serilog.Events;

namespace B2.Server.Service.Serilog;

/// <summary>Bounded in-memory sink for the F-18 log viewer (S1: store last 1000 events).</summary>
public sealed class InMemoryLogStore : ILogEventSink
{
    private readonly object _lock = new();
    private readonly Queue<StoredLogEvent> _queue = new();
    private const int MaxEntries = 1000;

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            while (_queue.Count >= MaxEntries)
            {
                _queue.Dequeue();
            }

            _queue.Enqueue(new StoredLogEvent(logEvent.Timestamp, logEvent.Level, logEvent.MessageTemplate.Text));
        }
    }

    public IReadOnlyList<StoredLogEvent> Snapshot()
    {
        lock (_lock)
        {
            return _queue.ToArray();
        }
    }
}

public readonly record struct StoredLogEvent(DateTimeOffset At, LogEventLevel Level, string MessageTemplate);
