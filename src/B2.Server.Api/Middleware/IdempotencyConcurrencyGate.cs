using System.Collections.Concurrent;

namespace B2.Server.Api.Middleware;

/// <summary>
/// Serializes idempotent POST handling for the same logical request (idempotency key + body hash).
/// Parallel retries can otherwise both miss the replay row and race through the handler (FR-019),
/// surfacing as duplicate writes or unique-index failures on declaration rows.
/// </summary>
public sealed class IdempotencyConcurrencyGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task RunSerializedAsync(string lockKey, Func<Task> action, CancellationToken cancellationToken)
    {
        var sem = _locks.GetOrAdd(lockKey, static _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            sem.Release();
        }
    }
}
