namespace B2.Application.Admin.LogViewer;

/// <summary>One captured log line surfaced by the F-18 in-app log viewer.</summary>
public sealed record LogEntry(DateTimeOffset At, string Level, string Message);
