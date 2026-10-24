using System.Security.Cryptography;
using System.Text;
using B2.Infrastructure.IO;
using Microsoft.IdentityModel.Tokens;

namespace B2.Infrastructure.Auth;

/// <summary>
/// Loads or creates RSA signing key. Windows: DPAPI-sealed blob under data/keys.
/// Non-Windows: dev PEM file (CI/local only; not for production deployment).
/// </summary>
public sealed class RsaJwtKeyProvider
{
    private readonly string _keysDirectory;

    private readonly object _gate = new();
    private volatile RsaSecurityKey? _cached;

    public RsaJwtKeyProvider(string dataDirectory)
    {
        _keysDirectory = Path.Combine(dataDirectory, "keys");
    }

    /// <summary>
    /// #148: cold-start requests race in here. The lock guarantees a single load/create, so no
    /// thread signs with a key pair the validator never sees. A failed load is not cached, so
    /// the next call retries (unlike <c>Lazy&lt;T&gt;</c>, which would memoise the exception).
    /// </summary>
    public RsaSecurityKey GetSigningKey()
    {
        if (_cached is { } key)
        {
            return key;
        }

        lock (_gate)
        {
            return _cached ??= LoadOrCreate();
        }
    }

    private RsaSecurityKey LoadOrCreate()
    {
        Directory.CreateDirectory(_keysDirectory);
        var dpapiPath = Path.Combine(_keysDirectory, "jwt-rsa.key.dpapi");
        var pemDevPath = Path.Combine(_keysDirectory, "jwt-rsa.dev.pem");

        if (OperatingSystem.IsWindows())
        {
            if (!File.Exists(dpapiPath))
            {
                using var rsa = RSA.Create(2048);
                var pem = rsa.ExportPkcs8PrivateKeyPem();
                var protectedBytes = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(pem),
                    optionalEntropy: null,
                    scope: DataProtectionScope.LocalMachine);
                AtomicFile.WriteAllBytes(dpapiPath, protectedBytes);
            }

            var pemText = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(
                    File.ReadAllBytes(dpapiPath),
                    optionalEntropy: null,
                    scope: DataProtectionScope.LocalMachine));
            var rsaImport = RSA.Create();
            rsaImport.ImportFromPem(pemText);
            return new RsaSecurityKey(rsaImport);
        }
        else
        {
            if (!File.Exists(pemDevPath))
            {
                using var rsa = RSA.Create(2048);
                AtomicFile.WriteAllText(pemDevPath, rsa.ExportPkcs8PrivateKeyPem());
            }

            var pemText = File.ReadAllText(pemDevPath);
            var rsaImport = RSA.Create();
            rsaImport.ImportFromPem(pemText);
            return new RsaSecurityKey(rsaImport);
        }
    }
}
