using B2.Application.Abstractions;
using B2.Shared.Contracts.Admin;

namespace B2.Application.Admin.LogViewer;

/// <summary>F-18 — paginated, newest-first read over the in-memory log buffer, optionally
/// filtered by level (Admin only).</summary>
public sealed class LogsQueryHandler
{
    private readonly ILogReader _reader;

    public LogsQueryHandler(ILogReader reader) => _reader = reader;

    public LogListResponse Handle(string? level, int page, int pageSize)
    {
        IEnumerable<LogEntry> entries = _reader.GetRecent();
        if (!string.IsNullOrWhiteSpace(level))
        {
            entries = entries.Where(e => string.Equals(e.Level, level, StringComparison.OrdinalIgnoreCase));
        }

        var all = entries.ToList();
        var items = all
            .AsEnumerable()
            .Reverse() // capture order is oldest-first; the viewer shows newest-first
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new LogEntryDto(e.At, e.Level, e.Message))
            .ToList();

        return new LogListResponse(page, pageSize, all.Count, items);
    }
}
