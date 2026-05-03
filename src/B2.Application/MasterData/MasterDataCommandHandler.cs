using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.MasterData;
using B2.Shared.Contracts.MasterData;

namespace B2.Application.MasterData;

public sealed class MasterDataCommandHandler
{
    private readonly IProcessorRepository _processors;
    private readonly IYardRepository _yards;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;

    public MasterDataCommandHandler(
        IProcessorRepository processors,
        IYardRepository yards,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit)
    {
        _processors = processors;
        _yards = yards;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<(IReadOnlyList<ProcessorDto> Items, int Total)> ListProcessorsAsync(
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _processors.ListAsync(includeInactive, page, pageSize, cancellationToken);
        return (items.Select(p => new ProcessorDto(p.Id, p.FullName, p.IsActive)).ToList(), total);
    }

    public async Task<ProcessorDto> CreateProcessorAsync(Guid actorUserId, ProcessorWriteRequest request, CancellationToken cancellationToken)
    {
        if (await _processors.FullNameExistsAsync(request.FullName, null, cancellationToken))
        {
            throw new DomainException("PROCESSOR_DUPLICATE", "Processor full name already exists.", 409);
        }

        var p = new Processor(Guid.NewGuid(), request.FullName, _clock.UtcNow);
        if (request.IsActive is false)
        {
            p.SetActive(false);
        }

        await _processors.AddAsync(p, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { p.FullName, p.IsActive });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Create, nameof(Processor), p.Id, null, after, null));
        return new ProcessorDto(p.Id, p.FullName, p.IsActive);
    }

    public async Task<ProcessorDto> UpdateProcessorAsync(Guid actorUserId, Guid id, ProcessorWriteRequest request, CancellationToken cancellationToken)
    {
        var p = await _processors.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException("PROCESSOR_NOT_FOUND", "Processor not found.", 404);

        if (await _processors.FullNameExistsAsync(request.FullName, id, cancellationToken))
        {
            throw new DomainException("PROCESSOR_DUPLICATE", "Processor full name already exists.", 409);
        }

        var before = System.Text.Json.JsonSerializer.Serialize(new { p.FullName, p.IsActive });
        p.Rename(request.FullName);
        if (request.IsActive is { } a)
        {
            p.SetActive(a);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { p.FullName, p.IsActive });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(Processor), p.Id, before, after, null));
        return new ProcessorDto(p.Id, p.FullName, p.IsActive);
    }

    public async Task DeactivateProcessorAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken)
    {
        var p = await _processors.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException("PROCESSOR_NOT_FOUND", "Processor not found.", 404);

        var before = System.Text.Json.JsonSerializer.Serialize(new { p.IsActive });
        p.SetActive(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { p.IsActive });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(Processor), p.Id, before, after, null));
    }

    public async Task<(IReadOnlyList<YardDto> Items, int Total)> ListYardsAsync(
        bool includeInactive,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _yards.ListAsync(includeInactive, page, pageSize, cancellationToken);
        return (items.Select(y => new YardDto(y.Id, y.Code, y.Name, y.IsActive)).ToList(), total);
    }

    public async Task<YardDto> CreateYardAsync(Guid actorUserId, YardWriteRequest request, CancellationToken cancellationToken)
    {
        if (await _yards.CodeExistsAsync(request.Code, null, cancellationToken))
        {
            throw new DomainException("YARD_CODE_DUPLICATE", "Yard code already exists.", 409);
        }

        var y = new Yard(Guid.NewGuid(), request.Code, request.Name, _clock.UtcNow);
        if (request.IsActive is false)
        {
            y.SetActive(false);
        }

        await _yards.AddAsync(y, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { y.Code, y.Name, y.IsActive });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Create, nameof(Yard), y.Id, null, after, null));
        return new YardDto(y.Id, y.Code, y.Name, y.IsActive);
    }

    public async Task<YardDto> UpdateYardAsync(Guid actorUserId, Guid id, YardWriteRequest request, CancellationToken cancellationToken)
    {
        var y = await _yards.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException("YARD_NOT_FOUND", "Yard not found.", 404);

        if (await _yards.CodeExistsAsync(request.Code, id, cancellationToken))
        {
            throw new DomainException("YARD_CODE_DUPLICATE", "Yard code already exists.", 409);
        }

        var before = System.Text.Json.JsonSerializer.Serialize(new { y.Code, y.Name, y.IsActive });
        y.Update(request.Code, request.Name);
        if (request.IsActive is { } a)
        {
            y.SetActive(a);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { y.Code, y.Name, y.IsActive });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(Yard), y.Id, before, after, null));
        return new YardDto(y.Id, y.Code, y.Name, y.IsActive);
    }

    public async Task DeactivateYardAsync(Guid actorUserId, Guid id, CancellationToken cancellationToken)
    {
        var y = await _yards.GetByIdAsync(id, cancellationToken)
            ?? throw new DomainException("YARD_NOT_FOUND", "Yard not found.", 404);

        var before = System.Text.Json.JsonSerializer.Serialize(new { y.IsActive });
        y.SetActive(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var after = System.Text.Json.JsonSerializer.Serialize(new { y.IsActive });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Update, nameof(Yard), y.Id, before, after, null));
    }
}
