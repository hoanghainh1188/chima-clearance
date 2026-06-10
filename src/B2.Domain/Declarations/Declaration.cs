using B2.Domain.Users;

namespace B2.Domain.Declarations;

public sealed class Declaration
{
#pragma warning disable CS8618
    private Declaration()
    {
    }
#pragma warning restore CS8618

    public Guid Id { get; private set; }
    public string DeclarationNumber { get; private set; } = null!;
    public Guid ProcessorId { get; private set; }
    public string CompanyName { get; private set; } = null!;
    public string GoodsName { get; private set; } = null!;
    public Guid YardId { get; private set; }
    public DeclarationStatus Status { get; private set; }
    public Guid EnteredByUserId { get; private set; }
    public DateTimeOffset EnteredAt { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public Guid? KvgsConfirmedByUserId { get; private set; }
    public DateTimeOffset? KvgsConfirmedAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<Vehicle> Vehicles { get; } = new();

    public static Declaration CreateNew(
        Guid id,
        DeclarationNumber declarationNumber,
        Guid processorId,
        string companyName,
        string goodsName,
        Guid yardId,
        Guid enteredByUserId,
        DateTimeOffset enteredAt,
        DateOnly businessDate)
    {
        return new Declaration
        {
            Id = id,
            DeclarationNumber = declarationNumber.Value,
            ProcessorId = processorId,
            CompanyName = companyName,
            GoodsName = goodsName,
            YardId = yardId,
            Status = DeclarationStatus.Open,
            EnteredByUserId = enteredByUserId,
            EnteredAt = enteredAt,
            BusinessDate = businessDate,
            RowVersion = new byte[8],
        };
    }

    public Vehicle AddVehicle(
        Guid vehicleId,
        PlateNumber plate,
        DateTimeOffset entryTime,
        Guid enteredByUserId,
        ContainerNumber? container,
        string? note)
    {
        EnsureCanAddVehicles();
        var trailer = container?.Value;
        var v = new Vehicle(vehicleId, Id, plate.Value, entryTime, enteredByUserId, trailer, note);
        v.AttachToDeclaration(this);
        Vehicles.Add(v);
        RecomputeStatus();
        return v;
    }

    public void RecomputeStatus()
    {
        if (Status is DeclarationStatus.KVGSConfirmed or DeclarationStatus.Cancelled)
        {
            return;
        }

        if (Vehicles.Count == 0)
        {
            Status = DeclarationStatus.Open;
            return;
        }

        var allExited = Vehicles.TrueForAll(v => v.ExitMarked);
        Status = allExited ? DeclarationStatus.AllExited : DeclarationStatus.Open;
    }

    /// <summary>INV-6: only Supervisor or Admin may transition AllExited → KVGSConfirmed.</summary>
    public void ConfirmKvgs(Role actorRole, Guid actorUserId, DateTimeOffset confirmedAtUtc)
    {
        if (actorRole is not Role.Admin and not Role.Supervisor)
        {
            throw new DomainException("FORBIDDEN", "Only Supervisor or Admin can confirm KVGS.", 403);
        }

        if (Status != DeclarationStatus.AllExited)
        {
            throw new DomainException(
                "DECLARATION_NOT_READY_FOR_KVGS",
                "Declaration must be AllExited before KVGS confirmation.",
                409);
        }

        if (!Vehicles.TrueForAll(v => v.ExitMarked))
        {
            throw new DomainException(
                "DECLARATION_NOT_READY_FOR_KVGS",
                "All vehicles must be exited before KVGS confirmation.",
                409);
        }

        Status = DeclarationStatus.KVGSConfirmed;
        KvgsConfirmedByUserId = actorUserId;
        KvgsConfirmedAt = confirmedAtUtc;
    }

    /// <summary>
    /// FR-022 — Admin-only cancellation, reachable only from <see cref="DeclarationStatus.Open"/>
    /// (INV-9) with a mandatory reason (INV-10). Soft-delete: the row is kept for audit /
    /// exports and rendered struck-through (FR-023); no further writes are permitted.
    /// </summary>
    public void Cancel(Role actorRole, Guid actorUserId, string reason, DateTimeOffset cancelledAtUtc)
    {
        if (actorRole != Role.Admin)
        {
            throw new DomainException("FORBIDDEN", "Only Admin can cancel a declaration.", 403);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("CANCELLATION_REASON_REQUIRED", "A cancellation reason is required.", 400);
        }

        if (Status != DeclarationStatus.Open)
        {
            throw new DomainException(
                "DECLARATION_NOT_CANCELLABLE",
                "Only an Open declaration can be cancelled.",
                409);
        }

        Status = DeclarationStatus.Cancelled;
        CancelledByUserId = actorUserId;
        CancelledAt = cancelledAtUtc;
        CancellationReason = reason.Trim();
    }

    private void EnsureCanAddVehicles()
    {
        if (Status == DeclarationStatus.KVGSConfirmed)
        {
            throw new DomainException("DECLARATION_LOCKED", "Cannot add vehicles to a KVGS-confirmed declaration.", 409);
        }

        if (Status == DeclarationStatus.Cancelled)
        {
            throw new DomainException("DECLARATION_LOCKED", "Cannot add vehicles to a cancelled declaration.", 409);
        }
    }
}
