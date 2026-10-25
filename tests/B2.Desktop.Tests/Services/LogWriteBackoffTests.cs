using B2.Desktop.Services.Logging;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

/// <summary>#171 — a locked log file pauses desktop logging briefly instead of disabling it.</summary>
public sealed class LogWriteBackoffTests
{
    [Fact]
    public void Writes_are_allowed_until_a_failure()
    {
        new LogWriteBackoff(new ManualTimeProvider()).CanWrite().Should().BeTrue();
    }

    [Fact]
    public void A_failure_pauses_writes_for_the_initial_delay_only()
    {
        var time = new ManualTimeProvider();
        var backoff = new LogWriteBackoff(time);

        backoff.RecordFailure();
        backoff.CanWrite().Should().BeFalse();

        time.Advance(LogWriteBackoff.InitialDelay);
        backoff.CanWrite().Should().BeTrue();
    }

    [Fact]
    public void Repeated_failures_double_the_pause_up_to_the_cap()
    {
        var time = new ManualTimeProvider();
        var backoff = new LogWriteBackoff(time);
        var expected = LogWriteBackoff.InitialDelay;

        for (var i = 0; i < 20; i++)
        {
            backoff.RecordFailure();
            time.Advance(expected - TimeSpan.FromMilliseconds(1));
            backoff.CanWrite().Should().BeFalse();
            time.Advance(TimeSpan.FromMilliseconds(1));
            backoff.CanWrite().Should().BeTrue();

            var doubled = expected + expected;
            expected = doubled < LogWriteBackoff.MaxDelay ? doubled : LogWriteBackoff.MaxDelay;
        }

        expected.Should().Be(LogWriteBackoff.MaxDelay);
    }

    [Fact]
    public void A_success_resets_the_pause_to_the_initial_delay()
    {
        var time = new ManualTimeProvider();
        var backoff = new LogWriteBackoff(time);
        backoff.RecordFailure();
        time.Advance(LogWriteBackoff.InitialDelay);
        backoff.RecordFailure();
        time.Advance(LogWriteBackoff.InitialDelay * 2);

        backoff.RecordSuccess();
        backoff.RecordFailure();
        time.Advance(LogWriteBackoff.InitialDelay);

        backoff.CanWrite().Should().BeTrue();
    }
}
