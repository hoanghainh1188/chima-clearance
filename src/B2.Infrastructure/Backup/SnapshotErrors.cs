using B2.Domain;

namespace B2.Infrastructure.Backup;

/// <summary>F-15 snapshot codec failures, surfaced as 400 problem+json by the global handler.</summary>
internal static class SnapshotErrors
{
    internal static DomainException Corrupt(string message) => new("BACKUP_CORRUPT", message, 400);

    internal static DomainException InvalidSecret() =>
        new("BACKUP_INVALID_SECRET", "Neither the password nor the recovery code can decrypt this snapshot.", 400);
}
