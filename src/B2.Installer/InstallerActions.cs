using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;

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
    private const int ApiPort = 5001; // HTTPS only (#147, ADR-0010)

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

        RestrictDataDirectoryAcl();
    }

    /// <summary>
    /// Locks <see cref="DataDir"/> down to SYSTEM (the service account) and Administrators.
    /// Left to inherit from C:\ProgramData, every local user could read keys\ — and all
    /// secrets there are DPAPI-sealed with LocalMachine scope, which any local account may
    /// unseal (JWT signing key, backup secrets, server.pfx). Setting a protected DACL on the
    /// root also re-propagates to existing children, so an upgrade fixes older installs.
    /// </summary>
    private static void RestrictDataDirectoryAcl()
    {
        const InheritanceFlags inheritToAll = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sidType in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(sidType, domainSid: null),
                FileSystemRights.FullControl,
                inheritToAll,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        new DirectoryInfo(DataDir).SetAccessControl(security);
    }

    /// <summary>
    /// Issues (or keeps) the HTTPS certificate in <c>keys\server.pfx</c> with SANs for this
    /// machine's hostname(s) and LAN IPv4 addresses, trusts it locally, and exports
    /// <c>b2-server.cer</c> for the exit-gate PCs. See <see cref="ServerCertificate"/>.
    /// </summary>
    public static bool InstallCertificate() => ServerCertificate.Ensure();

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
            // Upgrade path (deploy/install.ps1 re-run over an existing install): sc create
            // would fail with exit 1073 (service already exists) and abort the upgrade.
            // Update the binpath to the freshly copied executable instead.
            Run("sc.exe",
                $"config \"{ServiceName}\" binpath= \"{serviceExe}\" start= auto");
        }
        else
        {
            // sc.exe requires a space after every "option=" ("binpath= <value>"); without it
            // sc prints usage and exits non-zero, so a clean install could never register.
            Run("sc.exe",
                $"create \"{ServiceName}\" binpath= \"{serviceExe}\" " +
                $"start= auto obj= LocalSystem displayname= \"{ServiceDisplayName}\"");
        }

        Run("sc.exe", $"description \"{ServiceName}\" \"{ServiceDescription}\"");

        // Auto-restart: 5 s, 10 s, 30 s, then reset the failure count after 60 s uptime.
        Run("sc.exe",
            $"failure \"{ServiceName}\" reset= 60 " +
            "actions= restart/5000/restart/10000/restart/30000");
    }

    /// <summary>Opens TCP 5001 (HTTPS) inbound on the Private firewall profile for the API.</summary>
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

    /// <summary>Returns true if a Windows service with the given name is already registered.</summary>
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
            // sc.exe and netsh report failures on stdout, not stderr — include both.
            var output = $"{proc.StandardOutput.ReadToEnd()} {proc.StandardError.ReadToEnd()}".Trim();
            throw new InvalidOperationException(
                $"'{exe} {args}' exited {proc.ExitCode}: {output}");
        }
    }
}
