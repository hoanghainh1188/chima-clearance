using B2.Domain.Audit;

namespace B2.Application.Admin.Audit;

/// <summary>F-16 audit-viewer filter. All members optional; null = no constraint.</summary>
public sealed record AuditLogFilter(
    string? EntityType,
    Guid? EntityId,
    Guid? ActorUserId,
    AuditAction? Action,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc);
