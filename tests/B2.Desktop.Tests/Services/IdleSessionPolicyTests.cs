using B2.Desktop.Services;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

/// <summary>FR-018 / F-01 "Auto-logout on idle" — 30 minutes without input ends the session.</summary>
public sealed class IdleSessionPolicyTests
{
    [Fact]
    [Trait("Feature", "F-01")]
    [Trait("Requirement", "FR-018")]
    public void A_fresh_session_is_not_expired()
    {
        new IdleSessionPolicy(new ManualTimeProvider()).IsExpired().Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "F-01")]
    [Trait("Requirement", "FR-018")]
    public void Thirty_minutes_without_input_expires_the_session()
    {
        var time = new ManualTimeProvider();
        var idle = new IdleSessionPolicy(time);

        time.Advance(IdleSessionPolicy.Timeout - TimeSpan.FromSeconds(1));
        idle.IsExpired().Should().BeFalse();

        time.Advance(TimeSpan.FromSeconds(1));
        idle.IsExpired().Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-01")]
    [Trait("Requirement", "FR-018")]
    public void Input_restarts_the_idle_clock()
    {
        var time = new ManualTimeProvider();
        var idle = new IdleSessionPolicy(time);
        time.Advance(TimeSpan.FromMinutes(29));

        idle.RecordActivity();
        time.Advance(TimeSpan.FromMinutes(29));

        idle.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void The_check_interval_is_much_shorter_than_the_timeout()
    {
        IdleSessionPolicy.CheckInterval.Should().BeLessThan(IdleSessionPolicy.Timeout / 10);
    }
}
