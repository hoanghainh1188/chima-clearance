using B2.Shared.Contracts.Declarations;

namespace B2.Desktop.Services;

/// <summary>
/// FR-028 / Bug #163 — how the ledger polls for changes while SignalR is down. Kept free of
/// MAUI types so it is unit-tested in B2.Desktop.Tests.
/// <para>
/// The poll only asks "did anything change since the watermark?" (one-row probe); the grid
/// is left untouched unless it did, and then the CURRENT page is reloaded in full. The old
/// code rebuilt the grid from the delta itself — and an empty delta set TotalCount to 0,
/// which hid the grid behind "Không có dữ liệu" every 3 s.
/// </para>
/// </summary>
public static class LedgerDeltaPolicy
{
    /// <summary>Rows requested by the change probe; only TotalCount matters.</summary>
    public const int ProbePageSize = 1;

    /// <summary>
    /// Overlap subtracted from the server's as-of time, so a write committed while the
    /// previous query was reading is matched again rather than missed.
    /// </summary>
    public static readonly TimeSpan WatermarkOverlap = TimeSpan.FromSeconds(2);

    public static bool HasChanges(PagedDeclarationsResponse probe) =>
        probe.TotalCount > 0 || probe.Items.Count > 0;

    /// <summary>
    /// Next <c>since</c> watermark after a successful query. Prefers the server's as-of clock:
    /// it covers exits / KVGS confirmations and replaces the client-clock seed outright, so a
    /// PC clock running ahead of the server cannot park the watermark in the future. An older
    /// server without it falls back to the newest timestamp on the rows (never moving back).
    /// </summary>
    public static DateTimeOffset NextWatermark(DateTimeOffset current, PagedDeclarationsResponse response)
    {
        if (response.AsOfUtc is { } asOf)
        {
            return asOf - WatermarkOverlap;
        }

        var candidate = current;
        foreach (var item in response.Items)
        {
            candidate = Max(candidate, item.EnteredAt);
            if (item.KvgsConfirmedAt is { } confirmed)
            {
                candidate = Max(candidate, confirmed);
            }
        }

        return candidate;
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
}
