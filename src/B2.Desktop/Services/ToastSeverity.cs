namespace B2.Desktop.Services;

/// <summary>
/// Severity levels for <see cref="IToastService"/> per wireframe § 7. The
/// implementation translates each level into the auto-dismiss timing the
/// spec demands (4 s success, 8 s warning, never for error — manual dismiss).
/// </summary>
public enum ToastSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
}
