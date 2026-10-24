using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2.Application.Abstractions;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<AdminBootstrapStore> _logger;

    public AdminBootstrapStore(string dataDirectory, ILogger<AdminBootstrapStore> logger)
    {
        _keysDirectory = Path.Combine(dataDirectory, "keys");
        _logger = logger;
    }

    private string StatePath => Path.Combine(_keysDirectory, StateFileName);

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

    public AdminPasswordState? ReadState()
    {
        if (!File.Exists(StatePath))
        {
            return null;
        }

        return File.ReadAllText(StatePath).Trim() switch
        {
            "rotated" => AdminPasswordState.Rotated,
            "initial" => AdminPasswordState.Initial,
            _ => null,
        };
    }

    public void WriteState(AdminPasswordState state)
    {
        try
        {
            Directory.CreateDirectory(_keysDirectory);
            File.WriteAllText(StatePath, state == AdminPasswordState.Rotated ? "rotated" : "initial");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not fatal: the password change already succeeded. The bootstrapper re-derives the
            // state on the next start (MustChangePassword == false ⇒ rotated).
            _logger.LogWarning(ex, "F-01: could not record Admin password state {State} at {Path}.", state, StatePath);
        }
    }

    private sealed record Payload(string Password);
}
