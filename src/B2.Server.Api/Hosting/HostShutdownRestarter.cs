using Microsoft.Extensions.Hosting;

namespace B2.Server.Api.Hosting;

/// <summary>
/// Console / dev default: stops the host and leaves the next start to whoever launched it.
/// The installed Windows Service uses <see cref="WindowsServiceRestarter"/> instead.
/// </summary>
public sealed class HostShutdownRestarter : IServiceRestarter
{
    /// <summary>Grace period so the restore endpoint's HTTP 200 reaches the client first.</summary>
    internal static readonly TimeSpan ResponseFlushDelay = TimeSpan.FromSeconds(1);

    private readonly IHostApplicationLifetime _lifetime;

    public HostShutdownRestarter(IHostApplicationLifetime lifetime) => _lifetime = lifetime;

    public void RequestRestart() =>
        _ = Task.Run(
            async () =>
            {
                await Task.Delay(ResponseFlushDelay, CancellationToken.None).ConfigureAwait(false);
                _lifetime.StopApplication();
            },
            CancellationToken.None);
}
