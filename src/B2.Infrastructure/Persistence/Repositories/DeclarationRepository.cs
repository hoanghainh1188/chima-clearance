using B2.Application.Abstractions;
using B2.Domain.Declarations;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class DeclarationRepository : IDeclarationRepository
{
    private readonly B2DbContext _db;

    public DeclarationRepository(B2DbContext db)
    {
        _db = db;
    }

    public Task<bool> DeclarationNumberExistsAsync(string declarationNumber, CancellationToken cancellationToken) =>
        _db.Declarations.AnyAsync(d => d.DeclarationNumber == declarationNumber, cancellationToken);

    public async Task AddAsync(Declaration declaration, CancellationToken cancellationToken) =>
        await _db.Declarations.AddAsync(declaration, cancellationToken);
}
