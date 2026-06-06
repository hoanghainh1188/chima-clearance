using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace B2.Installer;

/// <summary>
/// FR-026 / T-S6-11 (T-S5-13 carry-over) — install-time recovery-code setup.
/// Generates the backup install-password and recovery code, DPAPI-seals them under
/// <c>C:\ProgramData\B2\keys\backup-secrets.dpapi</c>, then displays the recovery code
/// in the console and writes it to a temp file on the Desktop for the operator to copy.
///
/// The algorithm is intentionally inlined here (mirrors <c>RecoveryCodeGenerator</c> in
/// B2.Infrastructure) so that B2.Installer stays a lean standalone binary without pulling
/// in EF Core and the full Infrastructure dependency graph.
/// </summary>
internal static class RecoveryCodeSetup
{
    // Crockford base32 — no I/L/O/U to avoid transcription confusion.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int CodeLength = 24;

    private static string SealedPath =>
        Path.Combine(InstallerActions.DataDir, "keys", "backup-secrets.dpapi");

    /// <summary>
    /// Generates secrets if they don't exist, then prints the recovery code to stdout
    /// and writes a plaintext reminder to %USERPROFILE%\Desktop\B2-Recovery-Code.txt.
    /// Blocks until the operator presses Enter so they have time to copy the code.
    /// No-op if secrets are already initialized (safe to call on re-install).
    /// </summary>
    public static void InitializeAndDisplay()
    {
        if (File.Exists(SealedPath))
        {
            return;
        }

        var installPassword = GenerateCode();
        var recoveryCode = GenerateCode();

        PersistSealed(installPassword, recoveryCode);
        WriteDesktopReminder(recoveryCode);
        PrintAndWait(recoveryCode);
    }

    private static void PersistSealed(string installPassword, string recoveryCode)
    {
        var payload = new
        {
            InstallPassword = installPassword,
            RecoveryCode = recoveryCode,
            AcknowledgedAtInstall = false,
        };
        var json = JsonSerializer.Serialize(payload);
        var sealedBytes = System.Security.Cryptography.ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            optionalEntropy: null,
            scope: System.Security.Cryptography.DataProtectionScope.LocalMachine);
        File.WriteAllBytes(SealedPath, sealedBytes);
    }

    private static void WriteDesktopReminder(string recoveryCode)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var path = Path.Combine(desktop, "B2-Recovery-Code.txt");
            File.WriteAllText(
                path,
                $"MÃ PHỤC HỒI B2 CLEARANCE\n" +
                $"========================\n\n" +
                $"{FormatCode(recoveryCode)}\n\n" +
                $"Sao chép mã này vào phong bì dán kín.\n" +
                $"XÓA FILE NÀY sau khi đã lưu an toàn.\n",
                Encoding.UTF8);
        }
        catch (IOException)
        {
            // Non-fatal: console display below is the primary path.
        }
    }

    private static void PrintAndWait(string recoveryCode)
    {
        var border = new string('═', 62);
        Console.WriteLine();
        Console.WriteLine(border);
        Console.WriteLine("  MÃ PHỤC HỒI (Recovery Code) — GHI LẠI NGAY BÂY GIỜ");
        Console.WriteLine(border);
        Console.WriteLine();
        Console.WriteLine($"      {FormatCode(recoveryCode)}");
        Console.WriteLine();
        Console.WriteLine("  • Lưu mã trong phong bì dán kín tại phòng quản lý.");
        Console.WriteLine("  • Dùng để giải mã backup khi quên mật khẩu cài đặt.");
        Console.WriteLine("  • File nhắc nhở đã được tạo trên Desktop.");
        Console.WriteLine();
        Console.WriteLine(border);
        Console.Write("  Nhấn Enter sau khi đã ghi lại mã...");
        // Use a timeout so the installer doesn't hang forever if run non-interactively.
        var waitTask = Task.Run(() => Console.ReadLine());
        waitTask.Wait(TimeSpan.FromMinutes(5));
        Console.WriteLine();
    }

    private static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(CodeLength);
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }

    // Format as groups of 4: "ABCD EFGH IJKL MNOP QRST UVWX"
    private static string FormatCode(string code) =>
        string.Join(" ", Enumerable.Range(0, code.Length / 4)
            .Select(i => code.Substring(i * 4, 4)));
}
