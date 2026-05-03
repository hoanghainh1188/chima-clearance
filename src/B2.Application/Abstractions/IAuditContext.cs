using B2.Domain.Audit;

namespace B2.Application.Abstractions;

public readonly record struct AuditEnvelope(
    Guid? ActorUserId,
    AuditAction Action,
    string EntityType,
    Guid EntityId,
    string? BeforeJson,
    string? AfterJson,
    string? Reason);

public interface IAuditContext
{
    void Set(AuditEnvelope envelope);
}
