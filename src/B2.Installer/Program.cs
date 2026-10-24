using System.Text;
using B2.Installer;

// Standalone offline installer CLI (no update framework). Runs elevated (app.manifest
// requests Administrator). Invoked by deploy/install.ps1 after the payload has been copied
// to its permanent location, so AppContext.BaseDirectory points at the install folder that
// holds B2.Server.Service.exe, litestream.exe, and Templates\.
//
//   B2Clearance.exe install    → scaffold dirs, LAN TLS cert (+ b2-server.cer), service, firewall, recovery code, start
//   B2Clearance.exe uninstall  → stop + remove the service and firewall rule (keeps data)
//   B2Clearance.exe renew-certificate  → re-issue the HTTPS certificate if this PC's hostname / LAN
//                                        IP changed (or it nears expiry), then restart the service
//   B2Clearance.exe reset-initial-admin → re-issue the initial Admin password (F-01), only
//                                          while the Admin has not chosen their own yet
//
// All actions are idempotent, so re-running install over an existing deployment is safe.

// Vietnamese text and box-drawing characters; the legacy console code page mangles both.
Console.OutputEncoding = Encoding.UTF8;

var command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;

if (command == "install")
{
    var appDir = AppContext.BaseDirectory;
    InstallerActions.SetupDirectories();
    InstallerActions.InstallCertificate();
    InstallerActions.CopyTemplate(appDir);
    InstallerActions.CopyLitestream(appDir);
    InstallerActions.RegisterService(appDir);
    InstallerActions.AddFirewallRule();
    RecoveryCodeSetup.InitializeAndDisplay();
    AdminBootstrapSetup.InitializeAndDisplay();
    InstallerActions.StartService();
    Console.WriteLine("Cài đặt hoàn tất. Dịch vụ B2 đã được đăng ký và khởi động.");
    return 0;
}

if (command == "renew-certificate")
{
    if (InstallerActions.InstallCertificate())
    {
        InstallerActions.RestartService();
        Console.WriteLine("Đã khởi động lại dịch vụ B2 với chứng chỉ mới.");
    }

    return 0;
}

if (command == "reset-initial-admin")
{
    return AdminBootstrapSetup.ResetInitialAdmin();
}

if (command == "uninstall")
{
    InstallerActions.Uninstall();
    Console.WriteLine("Đã gỡ dịch vụ B2. Dữ liệu tại C:\\ProgramData\\B2 được giữ lại.");
    return 0;
}

var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "dev";
Console.WriteLine($"B2 Clearance Installer v{version}");
Console.WriteLine("Dùng: B2Clearance.exe install | uninstall | renew-certificate | reset-initial-admin   (chạy bằng quyền Administrator)");
Console.WriteLine("Thông thường bạn chạy deploy\\install.ps1 thay vì gọi trực tiếp.");
return 0;
