using System.Net;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace B2.Server.Api.Hosting;

/// <summary>
/// Issue #147 / #176 — the service talks to the exit-gate PC over the LAN, so it serves
/// HTTPS only (<c>https://0.0.0.0:5001</c>, <c>appsettings.json</c> <c>Urls</c>) with the
/// per-install certificate <c>B2Clearance.exe install</c> writes to <c>keys\server.pfx</c>.
/// Plain HTTP is refused on any non-loopback address at startup: credentials, JWTs and the
/// clearance data must never cross the LAN in cleartext (ADR-0010).
/// </summary>
public static class KestrelHttpsSetup
{
    /// <summary>Overrides the certificate location (default <c>{dataDir}\keys\server.pfx</c>).</summary>
    public const string CertificatePathKey = "B2:Https:CertificatePath";

    /// <summary>Certificate file name under <c>{dataDir}\keys\</c>, written by the installer.</summary>
    public const string CertificateFileName = "server.pfx";

    public static void Configure(WebApplicationBuilder builder, string dataDir)
    {
        var urlError = FindInsecureUrl(string.Join(';', ConfiguredUrls(builder.Configuration)));
        if (urlError is not null)
        {
            throw new InvalidOperationException(urlError);
        }

        if (!string.IsNullOrWhiteSpace(builder.Configuration["http_ports"]))
        {
            // .NET 8 ASPNETCORE_HTTP_PORTS binds http://*:{port} — always the LAN.
            throw new InvalidOperationException(
                "Refusing HTTP_PORTS: plain HTTP would send passwords and tokens over the LAN in "
                + "cleartext. Use Urls=https://0.0.0.0:5001.");
        }

        var certificatePath = ResolveCertificatePath(builder.Configuration[CertificatePathKey], dataDir);
        if (!File.Exists(certificatePath))
        {
            // Testing: WebApplicationFactory swaps Kestrel for TestServer, so no listener exists.
            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
            {
                // `dotnet run -- --console` on a dev box: Kestrel falls back to the ASP.NET Core
                // development certificate (`dotnet dev-certs https --trust`).
                return;
            }

            throw new InvalidOperationException(
                $"TLS certificate not found at '{certificatePath}'. Run 'B2Clearance.exe install' "
                + $"(or 'renew-certificate') as Administrator, or set {CertificatePathKey}.");
        }

        var certificate = LoadCertificate(certificatePath);
        // MachineKeySet without PersistKeySet imports a temporary key file into
        // ProgramData\Microsoft\Crypto\RSA\MachineKeys that is only deleted on Dispose. Kestrel
        // holds the certificate for the process lifetime, so release it on a clean SCM stop
        // instead of leaving one orphaned key file per service start.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => certificate.Dispose();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ConfigureHttpsDefaults(https =>
        {
            https.ServerCertificate = certificate;
            https.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
        }));
    }

    /// <summary><c>Urls</c> plus every <c>Kestrel:Endpoints:*:Url</c> — all the ways to bind.</summary>
    private static IEnumerable<string> ConfiguredUrls(ConfigurationManager configuration)
    {
        if (configuration["urls"] is { Length: > 0 } urls)
        {
            yield return urls;
        }

        foreach (var endpoint in configuration.GetSection("Kestrel:Endpoints").GetChildren())
        {
            if (endpoint["Url"] is { Length: > 0 } url)
            {
                yield return url;
            }
        }
    }

    public static string ResolveCertificatePath(string? configured, string dataDir) =>
        string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(dataDir, "keys", CertificateFileName)
            : configured.Trim();

    /// <summary>
    /// Returns an error message when <paramref name="urls"/> (Kestrel's <c>;</c>-separated
    /// <c>Urls</c> value) binds plain HTTP to anything but loopback; null when it is safe.
    /// Loopback HTTP is tolerated for troubleshooting — it never leaves the machine.
    /// </summary>
    public static string? FindInsecureUrl(string? urls)
    {
        if (string.IsNullOrWhiteSpace(urls))
        {
            return null;
        }

        foreach (var raw in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsLoopbackHost(HostOf(raw)))
            {
                return $"Refusing to listen on '{raw}': plain HTTP would send passwords and tokens "
                    + "over the LAN in cleartext. Use https:// (default https://0.0.0.0:5001).";
            }
        }

        return null;
    }

    private static X509Certificate2 LoadCertificate(string path)
    {
        // LocalSystem service: keep the imported private key in the machine key set, not a
        // per-user profile the service account may not have loaded.
        var flags = OperatingSystem.IsWindows()
            ? X509KeyStorageFlags.MachineKeySet
            : X509KeyStorageFlags.DefaultKeySet;
        var certificate = new X509Certificate2(path, (string?)null, flags);
        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException($"TLS certificate '{path}' has no private key.");
        }

        return certificate;
    }

    private static string HostOf(string url)
    {
        var authority = url[(url.IndexOf("://", StringComparison.Ordinal) + 3)..];
        var slash = authority.IndexOf('/');
        if (slash >= 0)
        {
            authority = authority[..slash];
        }

        if (authority.StartsWith('['))
        {
            var close = authority.IndexOf(']');
            return close > 0 ? authority[1..close] : authority;
        }

        var colon = authority.LastIndexOf(':');
        return colon >= 0 ? authority[..colon] : authority;
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip));
}
