using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2.Application.Abstractions;
using B2.Domain;

namespace B2.Infrastructure.Backup;

/// <summary>
/// FR-026 backup-secrets store. Generates the install password + recovery code on first
/// use and seals them under <c>{dataDirectory}/keys</c>. Windows: DPAPI (LocalMachine).
/// Non-Windows: a plain dev file (CI/local only; production is the Windows service) — same
/// pattern as <see cref="Auth.RsaJwtKeyProvider"/>.
/// </summary>
public sealed class BackupSecretsStore : IBackupSecretsStore
{
    private const int SecretLength = 24;

    private readonly object _gate = new();
    private readonly string _sealedPath;
    private readonly string _devPath;
    private BackupSecrets? _cached;

    public BackupSecretsStore(string dataDirectory)
    {
        var keys = Path.Combine(dataDirectory, "keys");
        _sealedPath = Path.Combine(keys, "backup-secrets.dpapi");
        _devPath = Path.Combine(keys, "backup-secrets.dev.json");
    }

    public BackupSecrets GetOrCreate()
    {
        lock (_gate)
        {
            if (_cached is not null)
            {
                return _cached;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_sealedPath)!);
            _cached = TryLoadIfPresent() ?? Persist(CreateNewSecrets());
            return _cached;
        }
    }

    public void MarkAcknowledged()
    {
        lock (_gate)
        {
            var current = _cached ?? TryLoadIfPresent();
            if (current is null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_sealedPath)!);
                current = Persist(CreateNewSecrets());
            }

            if (current.AcknowledgedAtInstall)
            {
                _cached = current;
                return;
            }

            _cached = Persist(current with { AcknowledgedAtInstall = true });
        }
    }

    private static BackupSecrets CreateNewSecrets() =>
        new(
            RecoveryCodeGenerator.Generate(SecretLength),
            RecoveryCodeGenerator.Generate(SecretLength),
            AcknowledgedAtInstall: false);

    /// <summary>Returns null when no secrets file exists yet; throws when a file exists but cannot be read.</summary>
    private BackupSecrets? TryLoadIfPresent()
    {
        if (!SecretsFileExists())
        {
            return null;
        }

        return ReadSecretsOrThrow();
    }

    private bool SecretsFileExists() =>
        OperatingSystem.IsWindows() ? File.Exists(_sealedPath) : File.Exists(_devPath);

    private BackupSecrets ReadSecretsOrThrow()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    File.ReadAllBytes(_sealedPath), optionalEntropy: null, scope: DataProtectionScope.LocalMachine));
                return DeserializeOrThrow(json);
            }

            return DeserializeOrThrow(File.ReadAllText(_devPath));
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or IOException)
        {
            throw new DomainException(
                "BACKUP_SECRETS_UNREADABLE",
                "The backup secrets file exists but could not be read. Existing encrypted snapshots would become undecryptable if new secrets were generated; restore the secrets file from backup or contact support.",
                503);
        }
    }

    private static BackupSecrets DeserializeOrThrow(string json) =>
        JsonSerializer.Deserialize<BackupSecrets>(json)
        ?? throw new DomainException(
            "BACKUP_SECRETS_UNREADABLE",
            "The backup secrets file exists but could not be parsed.",
            503);

    private BackupSecrets Persist(BackupSecrets secrets)
    {
        var json = JsonSerializer.Serialize(secrets);
        if (OperatingSystem.IsWindows())
        {
            var sealedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json), optionalEntropy: null, scope: DataProtectionScope.LocalMachine);
            WriteAtomically(_sealedPath, sealedBytes);
        }
        else
        {
            WriteAtomically(_devPath, Encoding.UTF8.GetBytes(json));
        }

        return secrets;
    }

    private static void WriteAtomically(string targetPath, byte[] bytes)
    {
        var tempPath = targetPath + ".tmp";
        File.WriteAllBytes(tempPath, bytes);
        File.Move(tempPath, targetPath, overwrite: true);
    }
}
