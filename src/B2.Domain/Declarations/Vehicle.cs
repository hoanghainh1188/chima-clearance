namespace B2.Domain.Declarations;

public sealed class Vehicle
{
#pragma warning disable CS8618
    private Vehicle()
    {
    }
#pragma warning restore CS8618

    internal Vehicle(
        Guid id,
        Guid declarationId,
        string plateNumber,
        DateTimeOffset entryTime,
        Guid enteredByUserId,
        string? trailerOrContainerNumber,
        string? note)
    {
        Id = id;
        DeclarationId = declarationId;
        PlateNumber = plateNumber;
        EntryTime = entryTime;
        EnteredByUserId = enteredByUserId;
        TrailerOrContainerNumber = trailerOrContainerNumber;
        Note = note;
        ExitMarked = false;
        RowVersion = new byte[8];
    }

    /// <summary>Wire parent navigation so EF Core tracks new vehicles as inserts on the declaration graph.</summary>
    internal void AttachToDeclaration(Declaration declaration)
    {
        Declaration = declaration;
    }

    public Guid Id { get; private set; }
    public Guid DeclarationId { get; private set; }
    public Declaration Declaration { get; private set; } = null!;
    public string PlateNumber { get; private set; } = null!;
    public string? PlateLast4 { get; private set; }
    public string? TrailerOrContainerNumber { get; private set; }
    public DateTimeOffset EntryTime { get; private set; }
    public bool ExitMarked { get; private set; }
    public DateTimeOffset? ExitTime { get; private set; }
    public Guid? ExitConfirmedByUserId { get; private set; }
    public string? Note { get; private set; }
    public Guid EnteredByUserId { get; private set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public void MarkExit(Guid confirmedByUserId, DateTimeOffset exitTime)
    {
        if (ExitMarked)
        {
            throw new DomainException(
                "VEHICLE_ALREADY_EXITED",
                "Vehicle is already marked as exited.",
                409);
        }

        EnsureDeclarationAllowsVehicleWrites();

        ExitMarked = true;
        ExitTime = exitTime;
        ExitConfirmedByUserId = confirmedByUserId;
    }

    public void UnExit()
    {
        if (!ExitMarked)
        {
            throw new DomainException("VEHICLE_NOT_EXITED", "Vehicle is not marked as exited.", 400);
        }

        EnsureDeclarationAllowsVehicleWrites();

        ExitMarked = false;
        ExitTime = null;
        ExitConfirmedByUserId = null;
    }

    private void EnsureDeclarationAllowsVehicleWrites()
    {
        if (Declaration is { Status: DeclarationStatus.Cancelled })
        {
            throw new DomainException(
                "DECLARATION_CANCELLED",
                "Cannot modify a vehicle on a cancelled declaration.",
                409);
        }

        if (Declaration is { Status: DeclarationStatus.KVGSConfirmed })
        {
            throw new DomainException(
                "DECLARATION_LOCKED",
                "Cannot modify a vehicle on a KVGS-confirmed declaration.",
                409);
        }
    }
}
