using B2.Desktop.Services;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

/// <summary>Bug #163 — the polling fallback must not blank the ledger or reload forever.</summary>
public sealed class LedgerDeltaPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 24, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_report_no_changes_for_an_empty_probe()
    {
        // The old code applied this response to the grid: TotalCount = 0 hid every row.
        LedgerDeltaPolicy.HasChanges(Page([], totalCount: 0)).Should().BeFalse();
    }

    [Fact]
    public void Should_report_changes_from_the_total_even_when_the_probe_page_is_short()
    {
        // The probe asks for one row; a change elsewhere still shows up in TotalCount.
        LedgerDeltaPolicy.HasChanges(Page([], totalCount: 3)).Should().BeTrue();
        LedgerDeltaPolicy.HasChanges(Page([Row(T0)], totalCount: 1)).Should().BeTrue();
    }

    [Fact]
    public void Should_take_the_server_as_of_time_minus_the_overlap()
    {
        var asOf = T0.AddMinutes(10);

        var next = LedgerDeltaPolicy.NextWatermark(T0, Page([Row(T0.AddMinutes(1))], 1, asOf));

        next.Should().Be(asOf - LedgerDeltaPolicy.WatermarkOverlap);
    }

    [Fact]
    public void Should_let_the_server_clock_replace_a_client_seed_that_runs_ahead()
    {
        // Desktop seeds the watermark from its own clock; a PC an hour fast must not park it
        // in the future and miss every change until the server catches up.
        var clientSeed = T0.AddHours(1);

        var next = LedgerDeltaPolicy.NextWatermark(clientSeed, Page([], 0, T0));

        next.Should().BeBefore(clientSeed);
    }

    [Fact]
    public void Should_fall_back_to_the_newest_row_timestamp_without_as_of()
    {
        var confirmed = T0.AddMinutes(30);
        var response = Page([Row(T0.AddMinutes(5)), Row(T0.AddMinutes(1), confirmed)], 2);

        LedgerDeltaPolicy.NextWatermark(T0, response).Should().Be(confirmed);
        LedgerDeltaPolicy.NextWatermark(T0.AddHours(1), response).Should().Be(T0.AddHours(1), "never moves back");
    }

    private static PagedDeclarationsResponse Page(
        IReadOnlyList<DeclarationSummaryDto> items,
        int totalCount,
        DateTimeOffset? asOf = null) =>
        new(items, totalCount, 1, LedgerDeltaPolicy.ProbePageSize, asOf);

    private static DeclarationSummaryDto Row(DateTimeOffset enteredAt, DateTimeOffset? kvgsConfirmedAt = null) =>
        new(
            Guid.NewGuid(),
            "TK-1",
            "Công ty A",
            "Hàng B",
            kvgsConfirmedAt is null ? "AllExited" : "KVGSConfirmed",
            1,
            0,
            Guid.NewGuid(),
            "Xử lý",
            Guid.NewGuid(),
            "B1",
            "Bãi 1",
            Guid.NewGuid(),
            "Nhân viên",
            enteredAt,
            DateOnly.FromDateTime(enteredAt.UtcDateTime),
            kvgsConfirmedAt is null ? null : Guid.NewGuid(),
            kvgsConfirmedAt,
            "30A12345",
            null,
            1);
}
