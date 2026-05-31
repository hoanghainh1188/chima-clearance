using System.Globalization;
using B2.Infrastructure.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace B2.Server.Service.Serilog;

/// <summary>
/// Serilog sink that forwards events into the shared F-18 <see cref="InMemoryLogReader"/>
/// (the bounded buffer the <c>/api/admin/logs</c> endpoint reads).
/// </summary>
public sealed class InMemoryLogStore : ILogEventSink
{
    private readonly InMemoryLogReader _reader;

    public InMemoryLogStore(InMemoryLogReader reader) => _reader = reader;

    public void Emit(LogEvent logEvent) =>
        _reader.Append(
            logEvent.Timestamp,
            logEvent.Level.ToString(),
            logEvent.RenderMessage(CultureInfo.InvariantCulture));
}
