namespace B2.Desktop.Services;

public interface IToastService
{
    /// <summary>
    /// Show a transient notification. Defaults to <see cref="ToastSeverity.Info"/>
    /// (4 s auto-dismiss).
    /// </summary>
    Task ShowAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Show a transient notification with explicit severity per wireframe § 7.
    /// Auto-dismiss timing: 4 s for Success/Info, 8 s for Warning, manual
    /// (close button) for Error.
    /// </summary>
    Task ShowAsync(string message, ToastSeverity severity, CancellationToken cancellationToken = default);
}
