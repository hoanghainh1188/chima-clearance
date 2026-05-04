using B2.Shared.Contracts.Declarations;

namespace B2.Application.Abstractions;

public interface IVehicleSuffixSearchRepository
{
    Task<IReadOnlyList<VehicleSearchResultDto>> SearchAsync(
        string suffix,
        bool unexitedOnly,
        int limit,
        CancellationToken cancellationToken);
}
