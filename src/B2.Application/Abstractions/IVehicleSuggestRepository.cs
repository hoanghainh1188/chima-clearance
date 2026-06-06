namespace B2.Application.Abstractions;

public interface IVehicleSuggestRepository
{
    Task<string?> SuggestAsync(string plate, string field, DateTimeOffset cutoff, CancellationToken cancellationToken);
}
