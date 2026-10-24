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
        EnsureDeclarationLoaded();
        if (ExitMarked)
        {
            throw new DomainException(
                "VEHICLE_ALREADY_EXITED",
                "Vehicle is already marked as exited.",
                409);
        }

        // FR-022: no vehicle under a cancelled declaration can be exited.
        if (Declaration is { Status: DeclarationStatus.Cancelled })
        {
            throw new DomainException(
                "DECLARATION_CANCELLED",
                "Cannot mark exit for a vehicle on a cancelled declaration.",
                409);
        }

        ExitMarked = true;
        ExitTime = exitTime;
        ExitConfirmedByUserId = confirmedByUserId;
    }

    public void UnExit()
    {
        EnsureDeclarationLoaded();
        if (!ExitMarked)
        {
            throw new DomainException("VEHICLE_NOT_EXITED", "Vehicle is not marked as exited.", 400);
        }

        // FR-022: cancelled declarations are immutable — no vehicle writes permitted.
        if (Declaration is { Status: DeclarationStatus.Cancelled })
        {
            throw new DomainException(
                "DECLARATION_CANCELLED",
                "Cannot un-exit a vehicle on a cancelled declaration.",
                409);
        }

        // FR-022: once KVGS is confirmed the declaration is terminal/locked — a later
        // un-exit would leave ExitMarked=false while Status=KVGSConfirmed (ledger drift).
        if (Declaration is { Status: DeclarationStatus.KVGSConfirmed })
        {
            throw new DomainException(
                "DECLARATION_LOCKED",
                "Cannot un-exit a vehicle on a KVGS-confirmed declaration.",
                409);
        }

        ExitMarked = false;
        ExitTime = null;
        ExitConfirmedByUserId = null;
    }

    /// <summary>
    /// Business rules § 3 — "Ghi chú" (cột O) is one of the five columns the exit-gate officer
    /// may fill, so the note is editable after entry (an empty value clears it). Every edit is
    /// audited by the caller; a cancelled declaration stays immutable like every other write.
    /// </summary>
    public void UpdateNote(string? note)
    {
        EnsureDeclarationLoaded();
        if (Declaration is { Status: DeclarationStatus.Cancelled })
        {
            throw new DomainException(
                "DECLARATION_CANCELLED",
                "Cannot edit the note of a vehicle on a cancelled declaration.",
                409);
        }

        // #183: KVGS confirmation seals the declaration (FR-022) — its ledger rows are final.
        if (Declaration is { Status: DeclarationStatus.KVGSConfirmed })
        {
            throw new DomainException(
                "DECLARATION_LOCKED",
                "Cannot edit the note of a vehicle on a KVGS-confirmed declaration.",
                409);
        }

        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>
    /// #139: every mutator guards on the parent's status, and <c>Declaration is { Status: … }</c>
    /// is silently false when the navigation was not loaded. Fail loudly instead of letting a
    /// cancelled / KVGS-locked declaration's vehicle be mutated.
    /// </summary>
    private void EnsureDeclarationLoaded()
    {
        if (Declaration is null)
        {
            throw new InvalidOperationException(
                "Vehicle.Declaration must be loaded before mutating the vehicle (status invariants).");
        }
    }
}
