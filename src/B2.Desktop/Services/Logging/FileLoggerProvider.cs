using Microsoft.Extensions.Logging;

namespace B2.Desktop.Services.Logging;

/// <summary>
/// A daily log file under <c>%LOCALAPPDATA%\B2\logs</c>. The desktop had no logging provider at
/// all outside <c>#if DEBUG</c>, so anything the app logged in a shipped build — a bad server
/// address in <c>b2-client.json</c>, a dropped connection — went nowhere and the operator was
/// left with only "🔴 Mất kết nối" to reason from. The runbook points here.
/// <para>
/// LocalAppData, not ProgramData: the exit-gate PC is a client-only install with no
/// <c>C:\ProgramData\B2</c> and the app runs without administrator rights.
/// </para>
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _directory;
    private readonly object _gate = new();
    private bool _disabled;

    public FileLoggerProvider(string directory)
    {
        _directory = directory;
    }

    /// <summary><c>%LOCALAPPDATA%\B2\logs</c>.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "B2",
        "logs");

    public ILogger CreateLogger(string categoryName) =>
        new FileLogger(this, ShortCategory(categoryName));

    public void Dispose()
    {
        // Nothing retained between writes — each Append opens and closes the file.
    }

    /// <summary>
    /// Best-effort append. A logging failure must never surface to the operator or abort a
    /// clearance, so the first failure disables the provider for the rest of the session.
    /// </summary>
    internal void Append(string line)
    {
        if (_disabled)
        {
            return;
        }

        lock (_gate)
        {
            if (_disabled)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_directory);
                var file = Path.Combine(
                    _directory,
                    "desktop-" + DateTimeOffset.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture) + ".log");
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (IOException)
            {
                _disabled = true;
            }
            catch (UnauthorizedAccessException)
            {
                _disabled = true;
            }
            catch (ArgumentException)
            {
                _disabled = true;
            }
        }
    }

    /// <summary>Trims the namespace so lines stay readable: <c>B2.Desktop.Services.X</c> → <c>X</c>.</summary>
    private static string ShortCategory(string categoryName)
    {
        var lastDot = categoryName.LastIndexOf('.');
        return lastDot >= 0 && lastDot < categoryName.Length - 1
            ? categoryName[(lastDot + 1)..]
            : categoryName;
    }
}
