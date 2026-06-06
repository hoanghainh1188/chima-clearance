namespace B2.Application.Abstractions;

/// <summary>
/// F-15 restore sidecar written alongside <c>{db}.restore-pending</c> so the
/// <see cref="AuditAction.Restore"/> row can be persisted into the <b>restored</b>
/// database on the next startup (the HTTP request audit would otherwise be wiped when
/// the live DB file is replaced).
/// </summary>
public sealed record RestoreStagingMetadata(
    Guid? ActorUserId,
    DateTimeOffset StagedAtUtc,
    string? SnapshotFileName);
