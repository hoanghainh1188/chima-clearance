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

    /// <summary>
    /// Every number written into cột C of this parent row, ordered. Ordinal 0 is the primary and
    /// is cached on <see cref="DeclarationNumber"/> so existing lookups, ledger rows and reports
    /// keep working on a single value; INV-1 uniqueness is enforced across this list.
    /// </summary>
    public List<DeclarationNumberLine> Numbers { get; } = new();

    public static Declaration CreateNew(
        Guid id,
        IReadOnlyList<DeclarationNumber> declarationNumbers,
        Guid processorId,
        string companyName,
        string goodsName,
        Guid yardId,
        Guid enteredByUserId,
        DateTimeOffset enteredAt,
        DateOnly businessDate)
    {
        if (declarationNumbers is null || declarationNumbers.Count == 0)
        {
            throw new DomainException(
                "DECLARATION_NUMBER_REQUIRED",
                "A declaration needs at least one declaration number.",
                400);
        }

        var distinct = declarationNumbers.Select(n => n.Value).Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count != declarationNumbers.Count)
        {
            throw new DomainException(
                "DECLARATION_NUMBER_DUPLICATE",
                "The same declaration number cannot be listed twice on one row.",
                409);
        }

        var declaration = new Declaration
        {
            Id = id,
            DeclarationNumber = distinct[0],
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

        for (var i = 0; i < distinct.Count; i++)
        {
            declaration.Numbers.Add(new DeclarationNumberLine(Guid.NewGuid(), id, distinct[i], i));
        }

        return declaration;
    }

    /// <summary>
    /// Adds another number to cột C of this row. The cell is editable at the entry gate — a batch
    /// can gain a second tờ khai after the vehicles are already inside — so this is not
    /// creation-only. Uniqueness against other declarations is the repository's job (DB index);
    /// this guards the aggregate's own rules.
    /// </summary>
    public DeclarationNumberLine AddNumber(DeclarationNumber number)
    {
        EnsureNumbersEditable();

        if (Numbers.Exists(n => string.Equals(n.Number, number.Value, StringComparison.Ordinal)))
        {
            throw new DomainException(
                "DECLARATION_NUMBER_DUPLICATE",
                "That declaration number is already on this row.",
                409);
        }

        var line = new DeclarationNumberLine(Guid.NewGuid(), Id, number.Value, Numbers.Count);
        Numbers.Add(line);
        return line;
    }

    /// <summary>Removes a number from cột C. The row must keep at least one.</summary>
    public void RemoveNumber(string number)
    {
        EnsureNumbersEditable();

        var line = Numbers.Find(n => string.Equals(n.Number, number, StringComparison.Ordinal))
            ?? throw new DomainException(
                "DECLARATION_NUMBER_NOT_FOUND",
                "That declaration number is not on this row.",
                404);

        if (Numbers.Count == 1)
        {
            throw new DomainException(
                "DECLARATION_NUMBER_REQUIRED",
                "A declaration must keep at least one declaration number.",
                409);
        }

        Numbers.Remove(line);
        Numbers.Sort((a, b) => a.Ordinal.CompareTo(b.Ordinal));
        for (var i = 0; i < Numbers.Count; i++)
        {
            Numbers[i].SetOrdinal(i);
        }

        // Keep the cached primary in step with ordinal 0.
        DeclarationNumber = Numbers[0].Number;
    }

    private void EnsureNumbersEditable()
    {
        if (Status == DeclarationStatus.Cancelled)
        {
            throw new DomainException(
                "DECLARATION_CANCELLED",
                "Cannot edit the declaration numbers of a cancelled declaration.",
                409);
        }

        if (Status == DeclarationStatus.KVGSConfirmed)
        {
            throw new DomainException(
                "DECLARATION_LOCKED",
                "Cannot edit the declaration numbers after KVGS confirmation.",
                409);
        }
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

    /// <summary>
    /// INV-6: <see cref="DeclarationStatus.AllExited"/> → <see cref="DeclarationStatus.KVGSConfirmed"/>,
    /// performed by the exit-gate officer. No role restriction: business rules § B.2 has the same
    /// cán bộ tick cột J (xe ra) and then cột L (KVGS) and write their own name into cột N. The
    /// earlier Supervisor-only rule cited "Business rule cột L", which says no such thing, and it
    /// left no single non-Admin role able to work the exit gate.
    /// </summary>
    public void ConfirmKvgs(Guid actorUserId, DateTimeOffset confirmedAtUtc)
    {
        if (Status != DeclarationStatus.AllExited)
        {
            throw new DomainException(
                "DECLARATION_NOT_READY_FOR_KVGS",
                "Declaration must be AllExited before KVGS confirmation.",
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
        foreach (var number in Numbers)
        {
            // Frees every number for re-entry (INV-1 is scoped to non-cancelled rows).
            number.MarkCancelled();
        }

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
