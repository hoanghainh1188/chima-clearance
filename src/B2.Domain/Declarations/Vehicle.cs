namespace B2.Domain.Declarations;

public sealed class Vehicle
{
#pragma warning disable CS8618
    private Vehicle()
    {
    }
#pragma warning restore CS8618

    public Vehicle(
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
    }

    public Guid Id { get; set; }
    public Guid DeclarationId { get; set; }
    public Declaration Declaration { get; set; } = null!;
    public string PlateNumber { get; set; } = null!;
    public string? PlateLast4 { get; set; }
    public string? TrailerOrContainerNumber { get; set; }
    public DateTimeOffset EntryTime { get; set; }
    public bool ExitMarked { get; set; }
    public DateTimeOffset? ExitTime { get; set; }
    public Guid? ExitConfirmedByUserId { get; set; }
    public string? Note { get; set; }
    public Guid EnteredByUserId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
