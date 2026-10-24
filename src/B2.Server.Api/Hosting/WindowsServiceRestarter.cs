using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace B2.Server.Api.Hosting;

/// <summary>
/// Bug #173 — restarts the installed Windows Service after a restore.
/// <para>
/// <c>StopApplication()</c> is a clean stop (exit code 0), which the SCM does not treat as a
/// failure, so the installer's restart-on-failure actions never fire. Instead a detached
/// PowerShell helper waits for THIS process to exit (so the service is really
/// <c>Stopped</c>, however long Quartz / Kestrel / Litestream take to drain) and then retries
/// <c>Start-Service</c> until it succeeds. A fixed sleep before <c>sc start</c> raced the
/// shutdown and failed with error 1056 when the old process was still alive.
/// </para>
/// </summary>
public sealed class WindowsServiceRestarter : IServiceRestarter
{
    /// <summary>Must match <c>B2.Installer.InstallerActions.ServiceName</c>.</summary>
    public const string ServiceName = "B2ClearanceService";

    internal const int StartAttempts = 30;
    internal const int StartRetryDelaySeconds = 2;
    internal const int ProcessExitTimeoutSeconds = 300;

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<WindowsServiceRestarter> _logger;

    public WindowsServiceRestarter(IHostApplicationLifetime lifetime, ILogger<WindowsServiceRestarter> logger)
    {
        _lifetime = lifetime;
        _logger = logger;
    }

    public void RequestRestart() =>
        _ = Task.Run(
            async () =>
            {
                await Task.Delay(HostShutdownRestarter.ResponseFlushDelay, CancellationToken.None).ConfigureAwait(false);
                SpawnRestartHelper();
                _lifetime.StopApplication();
            },
            CancellationToken.None);

    private void SpawnRestartHelper()
    {
        try
        {
            using var helper = Process.Start(BuildHelperStartInfo(Environment.ProcessId, ServiceName));
            _logger.LogInformation(
                "F-15 restore staged — restart helper {HelperPid} will start {Service} after process {Pid} exits.",
                helper?.Id,
                ServiceName,
                Environment.ProcessId);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The service still stops, so the restore is applied on the next manual start.
            _logger.LogError(ex, "F-15 restart helper could not be started; start {Service} manually.", ServiceName);
        }
    }

    /// <summary>Builds the detached helper that waits for <paramref name="processId"/> then starts the service.</summary>
    internal static ProcessStartInfo BuildHelperStartInfo(int processId, string serviceName)
    {
        var script = string.Format(
            CultureInfo.InvariantCulture,
            "Wait-Process -Id {0} -Timeout {1} -ErrorAction SilentlyContinue; " +
            "for ($i = 0; $i -lt {2}; $i++) {{ " +
            "try {{ Start-Service -Name '{3}' -ErrorAction Stop; exit 0 }} " +
            "catch {{ Start-Sleep -Seconds {4} }} }}; exit 1",
            processId,
            ProcessExitTimeoutSeconds,
            StartAttempts,
            serviceName,
            StartRetryDelaySeconds);

        var psi = new ProcessStartInfo("powershell.exe")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);
        return psi;
    }
}
