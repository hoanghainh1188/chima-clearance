namespace B2.Domain.Audit;

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string ChangesJson { get; set; } = null!;
    public string? Reason { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset At { get; set; }
    public string? CorrelationId { get; set; }
}
