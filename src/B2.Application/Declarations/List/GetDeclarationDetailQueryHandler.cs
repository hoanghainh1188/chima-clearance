using B2.Application.Abstractions;
using B2.Shared.Contracts.Declarations;

namespace B2.Application.Declarations.List;

public sealed class GetDeclarationDetailQueryHandler
{
    private readonly IDeclarationRepository _declarations;

    public GetDeclarationDetailQueryHandler(IDeclarationRepository declarations) => _declarations = declarations;

    public Task<DeclarationDetailResponse?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        _declarations.GetDeclarationDetailAsync(id, cancellationToken);
}
