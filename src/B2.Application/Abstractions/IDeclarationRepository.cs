using B2.Domain.Declarations;

namespace B2.Application.Abstractions;

public interface IDeclarationRepository
{
    Task<bool> DeclarationNumberExistsAsync(string declarationNumber, CancellationToken cancellationToken);
    Task AddAsync(Declaration declaration, CancellationToken cancellationToken);
}
