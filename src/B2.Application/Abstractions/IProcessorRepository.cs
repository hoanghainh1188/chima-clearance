using B2.Domain.MasterData;

namespace B2.Application.Abstractions;

public interface IProcessorRepository
{
    Task<Processor?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> FullNameExistsAsync(string fullName, Guid? exceptId, CancellationToken cancellationToken);
    Task AddAsync(Processor processor, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Processor> Items, int Total)> ListAsync(bool includeInactive, int page, int pageSize, CancellationToken cancellationToken);
}
