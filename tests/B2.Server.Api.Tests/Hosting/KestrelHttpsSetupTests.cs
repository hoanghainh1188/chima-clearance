using B2.Server.Api.Hosting;
using FluentAssertions;

namespace B2.Server.Api.Tests.Hosting;

/// <summary>#147 — the service must never put credentials on the LAN in cleartext.</summary>
public sealed class KestrelHttpsSetupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://0.0.0.0:5001")]
    [InlineData("https://0.0.0.0:5001;http://127.0.0.1:5000")]
    [InlineData("http://localhost:5000")]
    [InlineData("http://[::1]:5000")]
    [InlineData("HTTP://127.0.0.1:5000/")]
    public void Https_or_loopback_http_is_accepted(string? urls)
    {
        KestrelHttpsSetup.FindInsecureUrl(urls).Should().BeNull();
    }

    [Theory]
    [InlineData("http://0.0.0.0:5000")]
    [InlineData("http://*:5000")]
    [InlineData("http://+:5000")]
    [InlineData("http://192.168.1.10:5000")]
    [InlineData("http://[::]:5000")]
    [InlineData("https://0.0.0.0:5001; http://gate-pc:5000")]
    public void Plain_http_on_a_lan_address_is_refused(string urls)
    {
        KestrelHttpsSetup.FindInsecureUrl(urls).Should().NotBeNull()
            .And.Contain("http://");
    }

    [Fact]
    public void Certificate_defaults_to_the_installer_pfx_under_keys()
    {
        var dataDir = Path.Combine(Path.GetTempPath(), "b2-data");

        KestrelHttpsSetup.ResolveCertificatePath(null, dataDir)
            .Should().Be(Path.Combine(dataDir, "keys", "server.pfx"));
    }

    [Fact]
    public void Configured_certificate_path_wins()
    {
        KestrelHttpsSetup.ResolveCertificatePath("  /etc/b2/custom.pfx ", "/ignored")
            .Should().Be("/etc/b2/custom.pfx");
    }
}
