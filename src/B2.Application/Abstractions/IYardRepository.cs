using B2.Domain.MasterData;

namespace B2.Application.Abstractions;

public interface IYardRepository
{
    Task<Yard?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken cancellationToken);
    Task AddAsync(Yard yard, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Yard> Items, int Total)> ListAsync(bool includeInactive, int page, int pageSize, CancellationToken cancellationToken);
}
