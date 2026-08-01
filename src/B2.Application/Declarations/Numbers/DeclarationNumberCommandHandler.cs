using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.DomainEvents;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;

namespace B2.Application.Declarations.Numbers;

/// <summary>
/// Adds or removes a number on cột C of an existing parent row. Uniqueness (INV-1) is checked
/// against every non-cancelled number in the system before the aggregate is touched; the DB's
/// filtered unique index on <c>DeclarationNumbers</c> is the backstop for a lost race.
/// </summary>
public sealed class DeclarationNumberCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;
    private readonly ISignalRDomainEventDispatcher _realtime;

    public DeclarationNumberCommandHandler(
        IDeclarationRepository declarations,
        IUnitOfWork unitOfWork,
        IAuditContext audit,
        ISignalRDomainEventDispatcher realtime)
    {
        _declarations = declarations;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _realtime = realtime;
    }

    public async Task<IReadOnlyList<string>> AddAsync(
        AddDeclarationNumberCommand command,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var number = DeclarationNumber.Parse(command.Number);

        if (await _declarations.DeclarationNumberExistsAsync(number.Value, cancellationToken))
        {
            throw new DomainException(
                "DECLARATION_NUMBER_DUPLICATE",
                $"Declaration number {number.Value} already exists.",
                409);
        }

        var declaration = await LoadAsync(command.DeclarationId, cancellationToken);
        var before = Snapshot(declaration);
        declaration.AddNumber(number);

        return await CommitAsync(declaration, before, actorUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RemoveAsync(
        RemoveDeclarationNumberCommand command,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var declaration = await LoadAsync(command.DeclarationId, cancellationToken);
        var before = Snapshot(declaration);
        declaration.RemoveNumber((command.Number ?? string.Empty).Trim());

        return await CommitAsync(declaration, before, actorUserId, cancellationToken);
    }

    private async Task<Declaration> LoadAsync(Guid declarationId, CancellationToken cancellationToken) =>
        await _declarations.GetByIdWithVehiclesAsync(declarationId, cancellationToken)
        ?? throw new DomainException("DECLARATION_NOT_FOUND", "Declaration not found.", 404);

    private static string Snapshot(Declaration declaration) =>
        JsonSerializer.Serialize(new { DeclarationNumbers = Ordered(declaration) });

    private static List<string> Ordered(Declaration declaration) =>
        declaration.Numbers.OrderBy(n => n.Ordinal).Select(n => n.Number).ToList();

    private async Task<IReadOnlyList<string>> CommitAsync(
        Declaration declaration,
        string before,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var numbers = Ordered(declaration);
        var after = JsonSerializer.Serialize(new { DeclarationNumbers = numbers });
        _audit.Set(new AuditEnvelope(
            actorUserId,
            AuditAction.Update,
            nameof(Declaration),
            declaration.Id,
            before,
            after,
            null));
        await _realtime.PublishDeclarationLedgerChangedAsync(declaration.Id, cancellationToken);

        return numbers;
    }
}
