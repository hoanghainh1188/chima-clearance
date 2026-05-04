using System.Text.Json;
using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Application.DomainEvents;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Domain.MasterData;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;

namespace B2.Application.Declarations;

public sealed class CreateDeclarationCommandHandler
{
    private readonly IProcessorRepository _processors;
    private readonly IYardRepository _yards;
    private readonly IDeclarationRepository _declarations;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;
    private readonly ISignalRDomainEventDispatcher _realtime;

    public CreateDeclarationCommandHandler(
        IProcessorRepository processors,
        IYardRepository yards,
        IDeclarationRepository declarations,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit,
        ISignalRDomainEventDispatcher realtime)
    {
        _processors = processors;
        _yards = yards;
        _declarations = declarations;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _realtime = realtime;
    }

    public async Task<DeclarationDto> HandleAsync(DeclarationCreateRequest request, Guid enteredByUserId, CancellationToken cancellationToken)
    {
        var processor = await _processors.GetByIdAsync(request.ProcessorId, cancellationToken);
        if (processor is null || !processor.IsActive)
        {
            throw new DomainException("MASTER_PROCESSOR_NOT_FOUND", "Processor is missing or inactive.", 400);
        }

        var yard = await _yards.GetByIdAsync(request.YardId, cancellationToken);
        if (yard is null || !yard.IsActive)
        {
            throw new DomainException("MASTER_YARD_NOT_FOUND", "Yard is missing or inactive.", 400);
        }

        var declNo = DeclarationNumber.Parse(request.DeclarationNumber);
        if (await _declarations.DeclarationNumberExistsAsync(declNo.Value, cancellationToken))
        {
            throw new DomainException("DECLARATION_NUMBER_DUPLICATE", "Declaration number already exists.", 409);
        }

        var now = _clock.UtcNow;
        var businessDate = VietnamTime.BusinessDateFromUtc(now);
        var plate = PlateNumber.Parse(request.FirstVehicle.PlateNumber);
        var container = ContainerNumber.TryParse(request.FirstVehicle.TrailerOrContainerNumber);
        var note = string.IsNullOrWhiteSpace(request.FirstVehicle.Note) ? null : request.FirstVehicle.Note.Trim();

        var declId = Guid.NewGuid();
        var declaration = Declaration.CreateNew(
            declId,
            declNo,
            request.ProcessorId,
            request.CompanyName.Trim(),
            request.GoodsName.Trim(),
            request.YardId,
            enteredByUserId,
            now,
            businessDate);

        declaration.AddVehicle(Guid.NewGuid(), plate, now, enteredByUserId, container, note);

        await _declarations.AddAsync(declaration, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var after = JsonSerializer.Serialize(new { declaration.DeclarationNumber, declaration.Status });
        _audit.Set(new AuditEnvelope(enteredByUserId, AuditAction.Create, nameof(Declaration), declaration.Id, null, after, null));
        await _realtime.PublishDeclarationLedgerChangedAsync(declaration.Id, cancellationToken);

        return new DeclarationDto(
            declaration.Id,
            declaration.DeclarationNumber,
            new ProcessorDto(processor.Id, processor.FullName, processor.IsActive),
            declaration.CompanyName,
            declaration.GoodsName,
            new YardDto(yard.Id, yard.Code, yard.Name, yard.IsActive),
            declaration.Status.ToString(),
            declaration.EnteredByUserId,
            declaration.EnteredAt,
            declaration.BusinessDate,
            declaration.KvgsConfirmedByUserId,
            declaration.KvgsConfirmedAt,
            declaration.CancelledByUserId,
            declaration.CancelledAt,
            declaration.CancellationReason);
    }
}
