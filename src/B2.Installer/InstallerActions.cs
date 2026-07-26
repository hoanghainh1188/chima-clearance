using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace B2.Installer;

/// <summary>
/// Windows-specific install / uninstall actions: directory scaffold, self-signed TLS cert,
/// Windows Service registration, firewall rule, and asset copy.
/// All methods must be idempotent (safe to run more than once).
/// </summary>
internal static class InstallerActions
{
    internal const string ServiceName = "B2ClearanceService";
    internal const string DataDir = @"C:\ProgramData\B2";

    private const string ServiceDisplayName = "B2 Clearance Service";
    private const string ServiceDescription = "Hệ thống quản lý thông quan hàng hóa Cổng B2";
    private const string FirewallRuleName = "B2 Clearance API";
    private const int ApiPort = 5000;

    // -------------------------------------------------------------------------
    // Install
    // -------------------------------------------------------------------------

    public static void SetupDirectories()
    {
        foreach (var dir in new[]
        {
            Path.Combine(DataDir, "db"),
            Path.Combine(DataDir, "backup"),
            Path.Combine(DataDir, "archive"),
            Path.Combine(DataDir, "templates"),
            Path.Combine(DataDir, "keys"),
            Path.Combine(DataDir, "logs"),
        })
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// Generates a 10-year self-signed RSA-2048 / SHA-256 cert for localhost, stores the
    /// PFX (with private key) in <c>C:\ProgramData\B2\keys\server.pfx</c> for Kestrel, and
    /// installs the public cert into LocalMachine\Root so the Desktop trusts HTTPS.
    /// Skips if the PFX already exists (idempotent across re-installs).
    /// </summary>
    public static void InstallCertificate()
    {
        var pfxPath = Path.Combine(DataDir, "keys", "server.pfx");
        if (File.Exists(pfxPath))
        {
            return;
        }

        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=localhost, O=B2 Clearance, OU=Internal",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));
        req.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // serverAuth

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        req.CertificateExtensions.Add(sanBuilder.Build());

        var cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));

        // Save PFX (private key + cert) for Kestrel — no passphrase, protected by NTFS ACL.
        File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx));

        // Install public cert into LocalMachine\Root so Desktop trusts the TLS connection.
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        store.Add(cert);
        store.Close();
    }

    public static void CopyTemplate(string appDir)
    {
        var src = Path.Combine(appDir, "Templates", "Mẫu sổ B2.xlsx");
        var dst = Path.Combine(DataDir, "templates", "Mẫu sổ B2.xlsx");
        if (File.Exists(src))
        {
            File.Copy(src, dst, overwrite: true);
        }
    }

    /// <summary>Copies the bundled Litestream binary into the data directory if present.</summary>
    public static void CopyLitestream(string appDir)
    {
        var src = Path.Combine(appDir, "litestream.exe");
        var dst = Path.Combine(DataDir, "litestream.exe");
        if (File.Exists(src))
        {
            File.Copy(src, dst, overwrite: true);
        }
    }

    /// <summary>
    /// Registers B2.Server.Service.exe as a Windows Service with auto-start and
    /// three-stage failure restart policy (5 s / 10 s / 30 s).
    /// </summary>
    public static void RegisterService(string appDir)
    {
        var serviceExe = Path.Combine(appDir, "B2.Server.Service.exe");

        if (ServiceExists(ServiceName))
        {
            // Upgrade path: update binpath to the freshly copied service executable.
            Run("sc.exe",
                $"config \"{ServiceName}\" binpath= \"{serviceExe}\" start= auto");
        }
        else
        {
            Run("sc.exe",
                $"create \"{ServiceName}\" binpath=\"{serviceExe}\" " +
                $"start=auto obj=LocalSystem displayname=\"{ServiceDisplayName}\"");
        }

        Run("sc.exe", $"description \"{ServiceName}\" \"{ServiceDescription}\"");

        // Auto-restart: 5 s, 10 s, 30 s, then reset the failure count after 60 s uptime.
        Run("sc.exe",
            $"failure \"{ServiceName}\" reset=60 " +
            "actions=restart/5000/restart/10000/restart/30000");
    }

    /// <summary>Opens TCP 5000 inbound on the Private firewall profile for the API.</summary>
    public static void AddFirewallRule()
    {
        // Delete old rule first (idempotent).
        Run("netsh",
            $"advfirewall firewall delete rule name=\"{FirewallRuleName}\"",
            expectSuccess: false);

        Run("netsh",
            $"advfirewall firewall add rule name=\"{FirewallRuleName}\" " +
            $"dir=in action=allow protocol=TCP localport={ApiPort} profile=private");
    }

    public static void StartService()
    {
        Run("sc.exe", $"start \"{ServiceName}\"", expectSuccess: false);
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    public static void RestartService()
    {
        Run("sc.exe", $"stop \"{ServiceName}\"", expectSuccess: false);
        Thread.Sleep(3_000);
        Run("sc.exe", $"start \"{ServiceName}\"", expectSuccess: false);
    }

    // -------------------------------------------------------------------------
    // Uninstall
    // -------------------------------------------------------------------------

    public static void Uninstall()
    {
        Run("sc.exe", $"stop \"{ServiceName}\"", expectSuccess: false);
        Thread.Sleep(3_000);
        Run("sc.exe", $"delete \"{ServiceName}\"", expectSuccess: false);
        Run("netsh",
            $"advfirewall firewall delete rule name=\"{FirewallRuleName}\"",
            expectSuccess: false);
        // NOTE: C:\ProgramData\B2\ (database, backups, keys) is intentionally kept
        // so that a reinstall can recover existing data.
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool ServiceExists(string serviceName)
    {
        var psi = new ProcessStartInfo("sc.exe", $"query \"{serviceName}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit(30_000);
        return proc.ExitCode == 0;
    }

    private static void Run(string exe, string args, bool expectSuccess = true)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit(30_000);
        if (expectSuccess && proc.ExitCode != 0)
        {
            var stderr = proc.StandardError.ReadToEnd().Trim();
            throw new InvalidOperationException(
                $"'{exe} {args}' exited {proc.ExitCode}: {stderr}");
        }
    }
}
