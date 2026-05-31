namespace B2.Shared.Contracts.Admin;

/// <summary>One immutable audit row (F-16). <see cref="ChangesJson"/> holds the
/// before/after payload surfaced in the viewer's detail expander.</summary>
public sealed record AuditLogItemDto(
    Guid Id,
    DateTimeOffset At,
    Guid? ActorUserId,
    string? ActorUsername,
    string Action,
    string EntityType,
    Guid EntityId,
    string ChangesJson,
    string? Reason,
    string? CorrelationId);
