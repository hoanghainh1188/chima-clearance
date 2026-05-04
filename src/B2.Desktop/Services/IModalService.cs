namespace B2.Desktop.Services;

public interface IModalService
{
    /// <summary>
    /// Yes/No confirmation modal per wireframe § 7. Returns the user's
    /// choice: <c>true</c> for accept, <c>false</c> for cancel (or any
    /// non-destructive dismissal — Esc, backdrop click).
    /// </summary>
    /// <param name="destructive">
    /// When <c>true</c>, the popup cannot be dismissed by backdrop click.
    /// Used for destructive actions (e.g. Restore-from-backup) per § 7.
    /// </param>
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel,
        bool destructive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only modal with a single Close button per wireframe § 7. Used
    /// by the F1 cheat-sheet, error details, etc.
    /// </summary>
    Task ShowAsync(
        string title,
        string message,
        string close,
        CancellationToken cancellationToken = default);
}
