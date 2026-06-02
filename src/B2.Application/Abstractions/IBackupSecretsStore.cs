namespace B2.Application.Abstractions;

/// <summary>The install password + printable recovery code that wrap every backup's data
/// key (FR-026), plus whether the recovery code was acknowledged at install.</summary>
public sealed record BackupSecrets(string InstallPassword, string RecoveryCode, bool AcknowledgedAtInstall);

/// <summary>
/// Provisions and persists the backup secrets (FR-026). On first use it generates both
/// secrets and seals them to disk; thereafter it returns the persisted pair. Replaces the
/// dev-default secrets the backup endpoint used before this store existed.
/// </summary>
public interface IBackupSecretsStore
{
    BackupSecrets GetOrCreate();

    void MarkAcknowledged();
}
