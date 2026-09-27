using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2.Infrastructure.Auth;

namespace B2.Server.Api.Tests.Support;

/// <summary>
/// F-01 — stages the installer's pending initial Admin password in the form the service reads
/// on this OS: DPAPI-sealed (LocalMachine) on Windows, the plain dev file elsewhere. On the
/// Windows CI runner this exercises the real DPAPI read path.
/// </summary>
public static class AdminBootstrapFiles
{
    public static string PendingFileName =>
        OperatingSystem.IsWindows() ? AdminBootstrapStore.SealedFileName : AdminBootstrapStore.DevFileName;

    public static string PendingPath(string dataDirectory) =>
        Path.Combine(dataDirectory, "keys", PendingFileName);

    public static void StagePending(string dataDirectory, string password)
    {
        Directory.CreateDirectory(Path.Combine(dataDirectory, "keys"));
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Password = password }));
        var bytes = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(json, optionalEntropy: null, scope: DataProtectionScope.LocalMachine)
            : json;
        File.WriteAllBytes(PendingPath(dataDirectory), bytes);
    }
}
