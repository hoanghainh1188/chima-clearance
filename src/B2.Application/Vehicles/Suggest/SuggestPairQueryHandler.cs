using B2.Application.Abstractions;

namespace B2.Application.Vehicles.Suggest;

public sealed class SuggestPairQueryHandler
{
    private const int CutoffDays = 90;

    public static readonly IReadOnlySet<string> ValidFields =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "trailer", "company", "goods" };

    private readonly IVehicleSuggestRepository _repo;
    private readonly IClock _clock;

    public SuggestPairQueryHandler(IVehicleSuggestRepository repo, IClock clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public Task<string?> HandleAsync(string plate, string field, CancellationToken cancellationToken)
    {
        var cutoff = _clock.UtcNow.AddDays(-CutoffDays);
        return _repo.SuggestAsync(plate.Trim().ToUpperInvariant(), field.ToLowerInvariant(), cutoff, cancellationToken);
    }
}
