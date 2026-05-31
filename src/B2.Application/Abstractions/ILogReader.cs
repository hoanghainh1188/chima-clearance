using B2.Application.Admin.LogViewer;

namespace B2.Application.Abstractions;

/// <summary>
/// Reads the bounded in-memory ring buffer that mirrors recent Serilog output (F-18).
/// Entries are returned oldest-first in capture order.
/// </summary>
public interface ILogReader
{
    IReadOnlyList<LogEntry> GetRecent();
}
