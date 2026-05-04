using System.Text.Json;
using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Shared.Contracts.Declarations;

namespace B2.Application.Declarations;

public sealed class AddVehicleCommandHandler
{
    private readonly IDeclarationRepository _declarations;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;

    public AddVehicleCommandHandler(
        IDeclarationRepository declarations,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit)
    {
        _declarations = declarations;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task HandleAsync(
        Guid declarationId,
        VehicleAddRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var declaration = await _declarations.GetByIdWithVehiclesAsync(declarationId, cancellationToken)
            ?? throw new DomainException("DECLARATION_NOT_FOUND", "Declaration not found.", 404);

        var plate = PlateNumber.Parse(request.PlateNumber);
        var container = ContainerNumber.TryParse(request.TrailerOrContainerNumber);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var vehicle = declaration.AddVehicle(
            Guid.NewGuid(),
            plate,
            _clock.UtcNow,
            actorUserId,
            container,
            note);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var after = JsonSerializer.Serialize(new { vehicle.PlateNumber, vehicle.EntryTime });
        _audit.Set(new AuditEnvelope(actorUserId, AuditAction.Create, nameof(Vehicle), vehicle.Id, null, after, null));
    }
}
