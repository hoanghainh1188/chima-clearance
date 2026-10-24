using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace B2.Installer;

/// <summary>
/// Issue #147 / #176 — the self-signed TLS certificate Kestrel serves on TCP 5001 (ADR-0010).
/// <para>
/// The exit-gate PC reaches the service by LAN IP or hostname, so the certificate's SANs cover
/// <c>localhost</c>/loopback <b>and</b> this machine's hostname(s) and LAN IPv4 addresses. The
/// public half is exported to <see cref="PublicCertificatePath"/>; the operator carries it to
/// each exit-gate PC, where <c>install.ps1 -ServerCertificate</c> imports it into
/// LocalMachine\Root. No PC ever trusts anything but this one leaf (BasicConstraints CA=false).
/// </para>
/// <para>
/// Re-issued (new key) only when the existing certificate is missing, expires within
/// <see cref="RenewBefore"/>, or no longer covers the machine's current names — e.g. the LAN IP
/// changed. Otherwise <see cref="Ensure"/> is a no-op apart from re-exporting the .cer.
/// </para>
/// </summary>
internal static class ServerCertificate
{
    private const string Organization = "B2 Clearance";
    private static readonly TimeSpan RenewBefore = TimeSpan.FromDays(30);

    internal static string PfxPath => Path.Combine(InstallerActions.DataDir, "keys", "server.pfx");

    /// <summary>Public certificate (no private key) to copy to the exit-gate PCs.</summary>
    internal static string PublicCertificatePath => Path.Combine(InstallerActions.DataDir, "b2-server.cer");

    /// <summary>
    /// Makes sure <see cref="PfxPath"/> holds a valid certificate for this machine's current
    /// names, trusted locally, and exported to <see cref="PublicCertificatePath"/>.
    /// Returns true when a new certificate was issued (connected clients must import it).
    /// </summary>
    public static bool Ensure()
    {
        var names = SubjectNames.ForThisMachine();
        var existing = TryLoad(PfxPath);
        var issued = false;

        if (existing is null || !IsCurrent(existing, names))
        {
            var replacement = Issue(names);
            File.WriteAllBytes(PfxPath, replacement.Export(X509ContentType.Pfx));
            existing?.Dispose();
            existing = replacement;
            issued = true;
        }

        using (existing)
        {
            AddToRoot(existing);
            // Also covers a corrupt / deleted PFX whose predecessor is still trusted here.
            RemoveOtherB2CertificatesFromRoot(existing.Thumbprint);
            File.WriteAllBytes(PublicCertificatePath, existing.Export(X509ContentType.Cert));
            Print(existing, names, issued);
        }

        return issued;
    }

    private static X509Certificate2? TryLoad(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return new X509Certificate2(path, (string?)null, X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (CryptographicException)
        {
            // Corrupt / unreadable PFX: issue a fresh one rather than abort the install.
            return null;
        }
    }

    private static bool IsCurrent(X509Certificate2 certificate, SubjectNames names)
    {
        if (certificate.NotAfter.ToUniversalTime() - DateTime.UtcNow < RenewBefore)
        {
            return false;
        }

        var san = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        if (san is null)
        {
            return false;
        }

        var dns = san.EnumerateDnsNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ips = san.EnumerateIPAddresses().ToHashSet();
        return names.DnsNames.All(dns.Contains) && names.IpAddresses.All(ips.Contains);
    }

    /// <summary>10-year self-signed RSA-2048 / SHA-256 serverAuth leaf for <paramref name="names"/>.</summary>
    private static X509Certificate2 Issue(SubjectNames names)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            $"CN={names.PrimaryName}, O={Organization}, OU=Internal",
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
        foreach (var dns in names.DnsNames)
        {
            sanBuilder.AddDnsName(dns);
        }

        foreach (var ip in names.IpAddresses)
        {
            sanBuilder.AddIpAddress(ip);
        }

        req.CertificateExtensions.Add(sanBuilder.Build());

        return req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(10));
    }

    private static void AddToRoot(X509Certificate2 certificate)
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        // Public part only — the private key stays in server.pfx under the locked-down DataDir.
        using var publicOnly = new X509Certificate2(certificate.Export(X509ContentType.Cert));
        store.Add(publicOnly);
    }

    private static void RemoveOtherB2CertificatesFromRoot(string currentThumbprint)
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        foreach (var candidate in store.Certificates)
        {
            var isStaleB2 = candidate.Subject.Contains($"O={Organization}", StringComparison.Ordinal)
                && !string.Equals(candidate.Thumbprint, currentThumbprint, StringComparison.OrdinalIgnoreCase);
            if (isStaleB2)
            {
                store.Remove(candidate);
            }

            candidate.Dispose();
        }
    }

    private static void Print(X509Certificate2 certificate, SubjectNames names, bool issued)
    {
        Console.WriteLine();
        Console.WriteLine(issued
            ? "Đã cấp chứng chỉ TLS mới cho máy chủ B2 (HTTPS cổng 5001)."
            : "Chứng chỉ TLS hiện có vẫn hợp lệ — giữ nguyên.");
        Console.WriteLine($"  Tên máy / IP   : {string.Join(", ", names.DnsNames.Concat(names.IpAddresses.Select(ip => ip.ToString())))}");
        Console.WriteLine($"  Hết hạn        : {certificate.NotAfter:yyyy-MM-dd}");
        Console.WriteLine($"  Thumbprint     : {certificate.Thumbprint}");
        Console.WriteLine($"  Tệp cho máy cổng ra: {PublicCertificatePath}");
        if (names.LanIpv4Count == 0)
        {
            Console.WriteLine("  ⚠ Không tìm thấy địa chỉ IPv4 LAN nào — máy cổng ra chỉ kết nối được qua tên máy.");
        }

        if (issued)
        {
            Console.WriteLine("  → Chép tệp .cer trên sang từng máy cổng ra và chạy lại:");
            Console.WriteLine("    .\\install.ps1 -ClientOnly -ServerUrl <IP máy này> -ServerCertificate <đường dẫn .cer>");
        }

        Console.WriteLine();
    }

    /// <summary>The DNS names and IP addresses this machine is reachable by on the LAN.</summary>
    private sealed record SubjectNames(
        string PrimaryName,
        IReadOnlyList<string> DnsNames,
        IReadOnlyList<IPAddress> IpAddresses,
        int LanIpv4Count)
    {
        public static SubjectNames ForThisMachine()
        {
            var host = Dns.GetHostName();
            var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;

            var dns = new List<string> { "localhost", Environment.MachineName, host };
            if (!string.IsNullOrWhiteSpace(domain))
            {
                dns.Add($"{host}.{domain.Trim('.')}");
            }

            var lan = LanIpv4Addresses();
            var ips = new List<IPAddress> { IPAddress.Loopback, IPAddress.IPv6Loopback };
            ips.AddRange(lan);

            return new SubjectNames(
                Environment.MachineName,
                dns.Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ips.Distinct().ToList(),
                lan.Count);
        }

        /// <summary>
        /// Virtual / VPN adapters come and go (WSL, Hyper-V, Docker, a VPN client). Counting them
        /// would make every <c>install</c> that happens to run while one is up re-issue the
        /// certificate — and silently break every exit-gate PC until the new .cer is carried over.
        /// </summary>
        private static readonly string[] VirtualAdapterMarkers =
        [
            "Hyper-V", "vEthernet", "VirtualBox", "VMware", "WSL", "Docker", "VPN", "TAP-", "Wintun",
            "WireGuard", "Loopback", "Bluetooth",
        ];

        /// <summary>Unicast IPv4 on every up, physical Ethernet / Wi-Fi adapter (APIPA excluded).</summary>
        private static List<IPAddress> LanIpv4Addresses() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                    && nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet
                        or NetworkInterfaceType.GigabitEthernet
                        or NetworkInterfaceType.FastEthernetT
                        or NetworkInterfaceType.FastEthernetFx
                        or NetworkInterfaceType.Wireless80211
                    && !IsVirtual(nic))
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(unicast => unicast.Address)
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(ip)
                    && !(ip.GetAddressBytes() is [169, 254, ..]))
                .Distinct()
                .ToList();

        private static bool IsVirtual(NetworkInterface nic) =>
            VirtualAdapterMarkers.Any(marker =>
                nic.Name.Contains(marker, StringComparison.OrdinalIgnoreCase)
                || nic.Description.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
