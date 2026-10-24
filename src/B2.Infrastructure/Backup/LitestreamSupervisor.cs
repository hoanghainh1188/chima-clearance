using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace B2.Infrastructure.Backup;

/// <summary>
/// F-15 / T-S5-08 — supervises the Litestream child process for continuous backup. When
/// disabled or the binary is absent (dev / CI), it no-ops gracefully so the service still
/// starts; on the spec VM it runs <c>litestream replicate -config …</c> and restarts the
/// process (2 s backoff) if it crashes.
/// </summary>
public sealed class LitestreamSupervisor : IHostedService, IDisposable
{
    private static readonly TimeSpan RestartBackoff = TimeSpan.FromSeconds(2);

    private readonly LitestreamOptions _options;
    private readonly string _configPath;
    private readonly ILogger<LitestreamSupervisor> _logger;

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Process? _process;

    public LitestreamSupervisor(LitestreamOptions options, string dataDirectory, ILogger<LitestreamSupervisor> logger)
    {
        _options = options;
        _configPath = Path.Combine(dataDirectory, "litestream.generated.yml");
        _logger = logger;
    }

    /// <summary>True only when the replicate loop is actually running.</summary>
    public bool IsActive { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Litestream continuous backup is disabled.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_options.ExecutablePath) || !File.Exists(_options.ExecutablePath))
        {
            _logger.LogWarning(
                "Litestream executable not found at '{Path}'; continuous backup is off.",
                _options.ExecutablePath);
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(_options.DbPath) || _options.Replicas.Count == 0)
        {
            _logger.LogWarning("Litestream has no DB path or replica targets; continuous backup is off.");
            return Task.CompletedTask;
        }

        File.WriteAllText(_configPath, LitestreamConfigWriter.ToYaml(_options));
        _cts = new CancellationTokenSource();
        IsActive = true;
        _loop = SuperviseAsync(_cts.Token);
        _logger.LogInformation(
            "Litestream continuous backup started with {Count} replica target(s).",
            _options.Replicas.Count);
        return Task.CompletedTask;
    }

    private async Task SuperviseAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var startInfo = new ProcessStartInfo(_options.ExecutablePath, $"replicate -config \"{_configPath}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // #151: Litestream logs (including the reason it died) go to stderr/stdout;
                    // forward them so /api/admin/logs and the log files show why replication stopped.
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                // #170: release the previous child's OS handles before starting the next one.
                _process?.Dispose();
                _process = Process.Start(startInfo);
                if (_process is null)
                {
                    _logger.LogError("Failed to start the Litestream process.");
                }
                else
                {
                    _process.ErrorDataReceived += OnLitestreamOutput;
                    _process.OutputDataReceived += OnLitestreamOutput;
                    _process.BeginErrorReadLine();
                    _process.BeginOutputReadLine();
                    await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning("Litestream exited (code {Code}); restarting.", _process.ExitCode);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
            {
                _logger.LogError(ex, "Litestream process error; will retry after backoff.");
            }

            try
            {
                await Task.Delay(RestartBackoff, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnLitestreamOutput(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            _logger.LogInformation("Litestream: {Line}", e.Data);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();

        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Failed to stop the Litestream process cleanly.");
        }

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }

        IsActive = false;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _process?.Dispose();
    }
}
