using B2.Application.Abstractions;
using B2.Application.Admin.LogViewer;

namespace B2.Infrastructure.Diagnostics;

/// <summary>
/// Bounded (1000-entry) in-memory ring buffer behind the F-18 log viewer. The Serilog
/// sink (B2.Server.Service) calls <see cref="Append"/>; the log endpoint reads via
/// <see cref="ILogReader"/>. Registered once as a singleton so both share the buffer.
/// </summary>
public sealed class InMemoryLogReader : ILogReader
{
    private const int MaxEntries = 1000;
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries = new();

    public void Append(DateTimeOffset at, string level, string message)
    {
        lock (_gate)
        {
            while (_entries.Count >= MaxEntries)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(new LogEntry(at, level, message));
        }
    }

    public IReadOnlyList<LogEntry> GetRecent()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }
}
