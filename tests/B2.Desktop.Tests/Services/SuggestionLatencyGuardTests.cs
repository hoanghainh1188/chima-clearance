using B2.Desktop.Services;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

/// <summary>#160 — the F-17 latency guard suspends suggestions for a cooldown, not forever.</summary>
public sealed class SuggestionLatencyGuardTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(250);

    [Fact]
    [Trait("Feature", "F-17")]
    public void One_slow_round_trip_does_not_suspend()
    {
        var guard = new SuggestionLatencyGuard(new ManualTimeProvider());

        guard.RecordRoundTrip(Slow).Should().BeFalse();
        guard.CanQuery().Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public void A_fast_round_trip_resets_the_overrun_streak()
    {
        var guard = new SuggestionLatencyGuard(new ManualTimeProvider());

        guard.RecordRoundTrip(Slow);
        guard.RecordRoundTrip(Fast);

        guard.RecordRoundTrip(Slow).Should().BeFalse();
        guard.CanQuery().Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public void Two_consecutive_overruns_suspend_until_the_cooldown_elapses()
    {
        var time = new ManualTimeProvider();
        var guard = new SuggestionLatencyGuard(time);

        guard.RecordRoundTrip(Slow);
        guard.RecordRoundTrip(Slow).Should().BeTrue();
        guard.CanQuery().Should().BeFalse();

        time.Advance(SuggestionLatencyGuard.Cooldown - TimeSpan.FromSeconds(1));
        guard.CanQuery().Should().BeFalse();

        time.Advance(TimeSpan.FromSeconds(1));
        guard.CanQuery().Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public void After_the_cooldown_a_single_overrun_does_not_suspend_again()
    {
        var time = new ManualTimeProvider();
        var guard = new SuggestionLatencyGuard(time);
        guard.RecordRoundTrip(Slow);
        guard.RecordRoundTrip(Slow);
        time.Advance(SuggestionLatencyGuard.Cooldown);
        guard.CanQuery();

        guard.RecordRoundTrip(Slow).Should().BeFalse();
        guard.CanQuery().Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public void Exactly_the_budget_is_not_an_overrun()
    {
        var guard = new SuggestionLatencyGuard(new ManualTimeProvider());

        guard.RecordRoundTrip(SuggestionLatencyGuard.Budget);
        guard.RecordRoundTrip(SuggestionLatencyGuard.Budget).Should().BeFalse();
    }
}
