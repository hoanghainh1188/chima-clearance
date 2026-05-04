using System.ComponentModel;

namespace B2.Desktop.Services;

/// <summary>
/// Holds the correlation id of the most recent server response so the status bar
/// can surface it to operators (wireframe § 5: "Mã phiên: ..."). Captured by
/// <see cref="Api.CorrelationCaptureHandler"/> from the <c>X-Correlation-Id</c>
/// response header (see <c>CorrelationIdMiddleware</c> on the server).
/// </summary>
public interface ISessionCorrelationService : INotifyPropertyChanged
{
    string? CurrentCorrelationId { get; }

    void Capture(string? correlationId);
}
