namespace B2.Desktop.Services;

public interface IToastService
{
    Task ShowAsync(string message, CancellationToken cancellationToken = default);
}
