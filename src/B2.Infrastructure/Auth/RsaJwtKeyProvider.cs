using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace B2.Infrastructure.Auth;

/// <summary>
/// Loads or creates RSA signing key. Windows: DPAPI-sealed blob under data/keys.
/// Non-Windows: dev PEM file (CI/local only; not for production deployment).
/// </summary>
public sealed class RsaJwtKeyProvider
{
    private readonly string _keysDirectory;
    private RsaSecurityKey? _cached;

    public RsaJwtKeyProvider(string dataDirectory)
    {
        _keysDirectory = Path.Combine(dataDirectory, "keys");
    }

    public RsaSecurityKey GetSigningKey()
    {
        if (_cached is not null)
        {
            return _cached;
        }

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
                File.WriteAllBytes(dpapiPath, protectedBytes);
            }

            var pemText = Encoding.UTF8.GetString(
                ProtectedData.Unprotect(
                    File.ReadAllBytes(dpapiPath),
                    optionalEntropy: null,
                    scope: DataProtectionScope.LocalMachine));
            var rsaImport = RSA.Create();
            rsaImport.ImportFromPem(pemText);
            _cached = new RsaSecurityKey(rsaImport);
        }
        else
        {
            if (!File.Exists(pemDevPath))
            {
                using var rsa = RSA.Create(2048);
                File.WriteAllText(pemDevPath, rsa.ExportPkcs8PrivateKeyPem());
            }

            var pemText = File.ReadAllText(pemDevPath);
            var rsaImport = RSA.Create();
            rsaImport.ImportFromPem(pemText);
            _cached = new RsaSecurityKey(rsaImport);
        }

        return _cached;
    }
}
