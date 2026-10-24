namespace B2.Server.Api.Hosting;

/// <summary>
/// F-15 — restarts the host after a restore has been staged, so the pending snapshot is
/// applied by <see cref="B2ApiHost.InitializeDatabaseAsync"/> on the next start (Bug #173).
/// </summary>
public interface IServiceRestarter
{
    /// <summary>
    /// Schedules the restart and returns immediately, so the caller's HTTP 200 is flushed
    /// before the host begins shutting down.
    /// </summary>
    void RequestRestart();
}
