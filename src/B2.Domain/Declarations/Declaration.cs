namespace B2.Domain.Declarations;

public sealed class Declaration
{
    public Guid Id { get; set; }

#pragma warning disable CS8618
    private Declaration()
    {
    }
#pragma warning restore CS8618

    public Declaration(
        Guid id,
        string declarationNumber,
        Guid processorId,
        string companyName,
        string goodsName,
        Guid yardId,
        Guid enteredByUserId,
        DateTimeOffset enteredAt,
        DateOnly businessDate)
    {
        Id = id;
        DeclarationNumber = declarationNumber;
        ProcessorId = processorId;
        CompanyName = companyName;
        GoodsName = goodsName;
        YardId = yardId;
        Status = DeclarationStatus.Open;
        EnteredByUserId = enteredByUserId;
        EnteredAt = enteredAt;
        BusinessDate = businessDate;
    }
    public string DeclarationNumber { get; set; } = null!;
    public Guid ProcessorId { get; set; }
    public string CompanyName { get; set; } = null!;
    public string GoodsName { get; set; } = null!;
    public Guid YardId { get; set; }
    public DeclarationStatus Status { get; set; }
    public Guid EnteredByUserId { get; set; }
    public DateTimeOffset EnteredAt { get; set; }
    public DateOnly BusinessDate { get; set; }
    public Guid? KvgsConfirmedByUserId { get; set; }
    public DateTimeOffset? KvgsConfirmedAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<Vehicle> Vehicles { get; } = new();
}
