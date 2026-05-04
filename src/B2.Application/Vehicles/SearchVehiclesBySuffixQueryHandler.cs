using B2.Application.Abstractions;
using B2.Shared.Contracts.Declarations;

namespace B2.Application.Vehicles;

public sealed class SearchVehiclesBySuffixQueryHandler
{
    private const int DefaultLimit = 20;
    private readonly IVehicleSuffixSearchRepository _search;

    public SearchVehiclesBySuffixQueryHandler(IVehicleSuffixSearchRepository search) => _search = search;

    public Task<IReadOnlyList<VehicleSearchResultDto>> HandleAsync(
        string suffix,
        bool unexitedOnly,
        CancellationToken cancellationToken) =>
        _search.SearchAsync(suffix, unexitedOnly, DefaultLimit, cancellationToken);
}
