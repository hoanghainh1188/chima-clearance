using B2.Installer;

// Standalone offline installer CLI (no update framework). Runs elevated (app.manifest
// requests Administrator). Invoked by deploy/install.ps1 after the payload has been copied
// to its permanent location, so AppContext.BaseDirectory points at the install folder that
// holds B2.Server.Service.exe, litestream.exe, and Templates\.
//
//   B2Clearance.exe install    → scaffold dirs, TLS cert, service, firewall, recovery code, start
//   B2Clearance.exe uninstall  → stop + remove the service and firewall rule (keeps data)
//
// All actions are idempotent, so re-running install over an existing deployment is safe.

var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;

switch (command)
{
    case "install":
    {
        var appDir = AppContext.BaseDirectory;
        InstallerActions.SetupDirectories();
        InstallerActions.InstallCertificate();
        InstallerActions.CopyTemplate(appDir);
        InstallerActions.CopyLitestream(appDir);
        InstallerActions.RegisterService(appDir);
        InstallerActions.AddFirewallRule();
        RecoveryCodeSetup.InitializeAndDisplay();
        InstallerActions.StartService();
        Console.WriteLine("Cài đặt hoàn tất. Dịch vụ B2 đã được đăng ký và khởi động.");
        return 0;
    }

    case "uninstall":
    {
        InstallerActions.Uninstall();
        Console.WriteLine("Đã gỡ dịch vụ B2. Dữ liệu tại C:\\ProgramData\\B2 được giữ lại.");
        return 0;
    }

    default:
    {
        var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "dev";
        Console.WriteLine($"B2 Clearance Installer v{version}");
        Console.WriteLine("Dùng: B2Clearance.exe install | uninstall   (chạy bằng quyền Administrator)");
        Console.WriteLine("Thông thường bạn chạy deploy\\install.ps1 thay vì gọi trực tiếp.");
        return 0;
    }
}
