using B2.Desktop.Services;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

/// <summary>Bug #137 — the client must recover from 🔴 and bring SignalR back.</summary>
public sealed class ConnectivityPolicyTests
{
    [Fact]
    public void Should_keep_polling_while_the_server_is_unreachable()
    {
        var tick = ConnectivityPolicy.Decide(hubConnected: false, hubDisconnected: true, healthOk: false);

        tick.Mode.Should().Be(ConnectivityMode.Offline);
        tick.StopPolling.Should().BeFalse("the timer is the only thing that notices the LAN coming back");
        tick.TryRestartHub.Should().BeFalse();
        tick.SuggestRefresh.Should().BeFalse();
    }

    [Fact]
    public void Should_restart_a_closed_hub_once_http_is_back()
    {
        var tick = ConnectivityPolicy.Decide(hubConnected: false, hubDisconnected: true, healthOk: true);

        tick.Mode.Should().Be(ConnectivityMode.PollingFallback);
        tick.TryRestartHub.Should().BeTrue();
        tick.SuggestRefresh.Should().BeTrue();
        tick.StopPolling.Should().BeFalse();
    }

    [Fact]
    public void Should_leave_a_reconnecting_hub_to_its_own_retry_loop()
    {
        var tick = ConnectivityPolicy.Decide(hubConnected: false, hubDisconnected: false, healthOk: true);

        tick.Mode.Should().Be(ConnectivityMode.PollingFallback);
        tick.TryRestartHub.Should().BeFalse();
        tick.SuggestRefresh.Should().BeTrue();
    }

    [Fact]
    public void Should_go_online_and_stop_polling_when_the_hub_is_connected()
    {
        var tick = ConnectivityPolicy.Decide(hubConnected: true, hubDisconnected: false, healthOk: false);

        tick.Mode.Should().Be(ConnectivityMode.Online);
        tick.StopPolling.Should().BeTrue();
        tick.SuggestRefresh.Should().BeFalse();
    }

    [Fact]
    public void Should_bound_a_hub_restart_attempt_within_a_few_poll_intervals()
    {
        ConnectivityPolicy.HubStartTimeout.Should().BeGreaterThan(ConnectivityPolicy.PollInterval)
            .And.BeLessThanOrEqualTo(TimeSpan.FromSeconds(15));
    }
}
