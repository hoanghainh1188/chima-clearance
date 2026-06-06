using B2.Installer;
using Velopack;

// VelopackApp.Build().Run() MUST be the first call. It intercepts veloapp-install,
// veloapp-uninstall, and veloapp-updated before any app code executes.
VelopackApp.Build()
    .WithAfterInstallFastCallback(v =>
    {
        // Runs after Velopack has extracted all files. Set up infrastructure,
        // register the service, then generate and display the recovery code.
        var appDir = AppContext.BaseDirectory;
        InstallerActions.SetupDirectories();
        InstallerActions.InstallCertificate();
        InstallerActions.CopyTemplate(appDir);
        InstallerActions.CopyLitestream(appDir);
        InstallerActions.RegisterService(appDir);
        InstallerActions.AddFirewallRule();
        RecoveryCodeSetup.InitializeAndDisplay();
        InstallerActions.StartService();
    })
    .WithBeforeUninstallFastCallback(_ =>
    {
        InstallerActions.Uninstall();
    })
    .WithAfterUpdateFastCallback(_ =>
    {
        // After a Velopack self-update the new version files are in place;
        // restart the Windows Service so the new B2.Server.Service.exe picks up.
        InstallerActions.RestartService();
    })
    .Run();

// Normal run (no veloapp arg): print version and usage hint.
var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "dev";
Console.WriteLine($"B2 Clearance Installer v{version}");
Console.WriteLine("Chạy file setup.exe để cài đặt, hoặc dùng Add/Remove Programs để gỡ cài.");
