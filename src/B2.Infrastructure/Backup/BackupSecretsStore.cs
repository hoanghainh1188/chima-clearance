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
            _cached = TryLoad() ?? CreateAndPersist();
            return _cached;
        }
    }

    public void MarkAcknowledged()
    {
        lock (_gate)
        {
            var current = _cached ?? TryLoad();
            if (current is null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_sealedPath)!);
                current = CreateAndPersist();
            }

            if (current.AcknowledgedAtInstall)
            {
                _cached = current;
                return;
            }

            _cached = Persist(current with { AcknowledgedAtInstall = true });
        }
    }

    private BackupSecrets? TryLoad()
    {
        if (!SecretsFileExists())
        {
            return null;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    File.ReadAllBytes(_sealedPath), optionalEntropy: null, scope: DataProtectionScope.LocalMachine));
                return JsonSerializer.Deserialize<BackupSecrets>(json);
            }

            return JsonSerializer.Deserialize<BackupSecrets>(File.ReadAllText(_devPath));
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or IOException)
        {
            throw new DomainException(
                "BACKUP_SECRETS_UNREADABLE",
                "Backup secrets exist but could not be read. Refusing to rotate them automatically.",
                500);
        }
    }

    private bool SecretsFileExists() =>
        OperatingSystem.IsWindows() ? File.Exists(_sealedPath) : File.Exists(_devPath);

    private BackupSecrets CreateAndPersist() =>
        Persist(new BackupSecrets(
            RecoveryCodeGenerator.Generate(SecretLength),
            RecoveryCodeGenerator.Generate(SecretLength),
            AcknowledgedAtInstall: false));

    private BackupSecrets Persist(BackupSecrets secrets)
    {
        var json = JsonSerializer.Serialize(secrets);
        if (OperatingSystem.IsWindows())
        {
            var sealedBytes = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(json), optionalEntropy: null, scope: DataProtectionScope.LocalMachine);
            File.WriteAllBytes(_sealedPath, sealedBytes);
        }
        else
        {
            File.WriteAllText(_devPath, json);
        }

        return secrets;
    }
}
