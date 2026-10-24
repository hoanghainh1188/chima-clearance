using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2.Application.Common;

namespace B2.Installer;

/// <summary>
/// F-01 — per-install initial Admin password. Generates it, seals it with DPAPI for the
/// service to apply on its next start, and shows it on screen only (never written to a
/// plaintext file). File names and the JSON shape mirror
/// <c>B2.Infrastructure.Auth.AdminBootstrapStore</c>; the installer stays standalone and does
/// not reference Infrastructure.
///
/// The service records <c>keys\admin-password.state</c> on every start: <c>initial</c> while
/// the Admin has never chosen a password, <c>rotated</c> once they have. A rotated Admin
/// password is never replaced.
/// </summary>
internal static class AdminBootstrapSetup
{
    private const string SealedFileName = "admin-bootstrap.dpapi";
    private const string StateFileName = "admin-password.state";
    private const string RotatedState = "rotated";

    private static string KeysDirectory => Path.Combine(InstallerActions.DataDir, "keys");

    private static string DatabasePath => Path.Combine(InstallerActions.DataDir, "db", "b2.db");

    /// <summary>
    /// <c>install</c>: issues a new initial password on a fresh install, and on an upgrade
    /// while the Admin still has an initial password (including the shared password of
    /// v1.2.2 and earlier, which migration 0004 retires). Skips when the Admin has rotated.
    /// </summary>
    public static void InitializeAndDisplay()
    {
        var state = ReadState();
        if (state == RotatedState)
        {
            return;
        }

        // Upgrading from a build before F-01: no state file yet, so we cannot tell whether
        // the Admin already chose a password. The service ignores the new one if so.
        var unknownIfRotated = state is null && File.Exists(DatabasePath);
        var password = IssuePending();
        Display(password, unknownIfRotated);
    }

    /// <summary>
    /// <c>reset-initial-admin</c>: re-issues the initial password when the operator lost it
    /// before the first login. Refused once the Admin has chosen their own password.
    /// </summary>
    public static int ResetInitialAdmin()
    {
        var state = ReadState();
        if (state == RotatedState)
        {
            Console.WriteLine("Tài khoản admin đã đổi mật khẩu — không cấp lại mật khẩu ban đầu.");
            Console.WriteLine("Quên mật khẩu admin: nhờ một Admin khác đặt lại (xem runbook § 2).");
            return 2;
        }

        var password = IssuePending();
        InstallerActions.RestartService();
        Display(password, unknownIfRotated: state is null);
        return 0;
    }

    private static string? ReadState()
    {
        var path = Path.Combine(KeysDirectory, StateFileName);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static string IssuePending()
    {
        var password = InitialPasswordGenerator.Generate();
        var json = JsonSerializer.Serialize(new { Password = password });
        var sealedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            optionalEntropy: null,
            scope: DataProtectionScope.LocalMachine);
        Directory.CreateDirectory(KeysDirectory);
        File.WriteAllBytes(Path.Combine(KeysDirectory, SealedFileName), sealedBytes);
        return password;
    }

    private static void Display(string password, bool unknownIfRotated)
    {
        var border = new string('═', 62);
        Console.WriteLine();
        Console.WriteLine(border);
        Console.WriteLine("  TÀI KHOẢN QUẢN TRỊ BAN ĐẦU — GHI LẠI NGAY BÂY GIỜ");
        Console.WriteLine(border);
        Console.WriteLine();
        Console.WriteLine("      Tên đăng nhập:  admin");
        Console.WriteLine($"      Mật khẩu:       {password}");
        Console.WriteLine();
        Console.WriteLine("  • Mật khẩu chỉ hiện MỘT LẦN, không được lưu ra file.");
        Console.WriteLine("  • Đăng nhập ngay và đổi mật khẩu trong buổi cài.");
        Console.WriteLine("  • Mất trước khi đăng nhập: chạy B2Clearance.exe reset-initial-admin");
        if (unknownIfRotated)
        {
            Console.WriteLine("  • Nếu admin ĐÃ từng đổi mật khẩu, mật khẩu đó vẫn giữ nguyên —");
            Console.WriteLine("    khi đó bỏ qua mật khẩu trên.");
        }

        Console.WriteLine();
        Console.WriteLine(border);
        Console.Write("  Nhấn Enter sau khi đã ghi lại mật khẩu...");
        // Same non-interactive guard as the recovery-code prompt.
        var waitTask = Task.Run(() => Console.ReadLine());
        waitTask.Wait(TimeSpan.FromMinutes(5));
        Console.WriteLine();
    }
}
