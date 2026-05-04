namespace B2.Desktop.Services;

public interface IModalService
{
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept,
        string cancel,
        CancellationToken cancellationToken = default);
}
