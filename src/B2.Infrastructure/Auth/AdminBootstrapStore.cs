using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2.Application.Abstractions;

namespace B2.Infrastructure.Auth;

/// <summary>
/// F-01 — file-backed <see cref="IAdminBootstrapStore"/> under <c>{dataDirectory}/keys</c>.
/// The pending password is written by <c>B2Clearance.exe</c>: Windows seals it with DPAPI
/// (LocalMachine, protected by the data-directory ACL); non-Windows dev/CI uses a plain file,
/// same split as <see cref="Backup.BackupSecretsStore"/>. The state file is plain text
/// (<c>initial</c> / <c>rotated</c>) — it holds no secret.
/// </summary>
public sealed class AdminBootstrapStore : IAdminBootstrapStore
{
    public const string SealedFileName = "admin-bootstrap.dpapi";
    public const string DevFileName = "admin-bootstrap.dev.json";
    public const string StateFileName = "admin-password.state";

    private readonly string _keysDirectory;

    public AdminBootstrapStore(string dataDirectory)
    {
        _keysDirectory = Path.Combine(dataDirectory, "keys");
    }

    private string PendingPath => Path.Combine(
        _keysDirectory, OperatingSystem.IsWindows() ? SealedFileName : DevFileName);

    public string? TryReadPendingPassword()
    {
        if (!File.Exists(PendingPath))
        {
            return null;
        }

        try
        {
            var json = OperatingSystem.IsWindows()
                ? Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    File.ReadAllBytes(PendingPath), optionalEntropy: null, scope: DataProtectionScope.LocalMachine))
                : File.ReadAllText(PendingPath);
            var payload = JsonSerializer.Deserialize<Payload>(json);
            return string.IsNullOrEmpty(payload?.Password) ? null : payload.Password;
        }
        catch (Exception ex) when (ex is JsonException or CryptographicException or IOException)
        {
            throw new InvalidOperationException(
                $"Pending admin bootstrap file '{PendingPath}' is unreadable; run 'B2Clearance.exe reset-initial-admin'.",
                ex);
        }
    }

    public void DeletePending() => File.Delete(PendingPath);

    public void WriteState(AdminPasswordState state)
    {
        Directory.CreateDirectory(_keysDirectory);
        File.WriteAllText(
            Path.Combine(_keysDirectory, StateFileName),
            state == AdminPasswordState.Rotated ? "rotated" : "initial");
    }

    private sealed record Payload(string Password);
}
