using CommunityToolkit.Mvvm.ComponentModel;

namespace B2.Desktop.Services;

/// <summary>
/// Singleton storage for the latest <c>X-Correlation-Id</c> seen on a server
/// response. <see cref="Capture"/> is called from arbitrary HTTP threads, so
/// property updates are marshaled to the UI thread to keep XAML bindings happy.
/// </summary>
public sealed partial class SessionCorrelationService : ObservableObject, ISessionCorrelationService
{
    [ObservableProperty]
    private string? _currentCorrelationId;

    public void Capture(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return;
        }

        if (MainThread.IsMainThread)
        {
            CurrentCorrelationId = correlationId;
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => CurrentCorrelationId = correlationId);
    }
}
