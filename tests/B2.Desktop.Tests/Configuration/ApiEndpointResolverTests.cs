using B2.Desktop.Services;
using FluentAssertions;

namespace B2.Desktop.Tests.Configuration;

/// <summary>
/// The gate runs two PCs (business rules § B: cổng vào hosts the service, cổng ra is a LAN
/// client), so the desktop must be pointable at another machine without a rebuild.
/// </summary>
public sealed class ApiEndpointResolverTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(),
        "b2-endpoint-tests-" + Guid.NewGuid().ToString("N"));

    public ApiEndpointResolverTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // A locked temp file must not fail the suite.
        }
    }

    // -- Entry gate: nothing configured -------------------------------------------------

    [Fact]
    public void No_config_falls_back_to_loopback()
    {
        var result = ApiEndpointResolver.Resolve(_dir, environmentOverride: null);

        result.BaseUri.Should().Be(new Uri(ApiEndpointResolver.DefaultBaseAddress));
        result.Source.Should().Be(ApiEndpointSource.Default);
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void Missing_directory_falls_back_to_loopback()
    {
        var result = ApiEndpointResolver.Resolve(
            Path.Combine(_dir, "does-not-exist"),
            environmentOverride: null);

        result.BaseUri.Should().Be(new Uri(ApiEndpointResolver.DefaultBaseAddress));
        result.Source.Should().Be(ApiEndpointSource.Default);
    }

    // -- Exit gate: pointed at the entry-gate PC ----------------------------------------

    [Theory]
    [InlineData("192.168.1.10", "http://192.168.1.10:5000/")]
    [InlineData("192.168.1.10:5000", "http://192.168.1.10:5000/")]
    [InlineData("http://192.168.1.10:5000", "http://192.168.1.10:5000/")]
    [InlineData("http://192.168.1.10:5000/", "http://192.168.1.10:5000/")]
    [InlineData("  192.168.1.10  ", "http://192.168.1.10:5000/")]
    [InlineData("gate-pc", "http://gate-pc:5000/")]
    [InlineData("http://gate-pc:8080", "http://gate-pc:8080/")]
    [InlineData("https://gate-pc:5001", "https://gate-pc:5001/")]
    public void Config_file_address_is_normalised(string configured, string expected)
    {
        WriteConfig("{\"serverUrl\": \"" + configured.Trim() + "\"}");

        var result = ApiEndpointResolver.Resolve(_dir, environmentOverride: null);

        result.BaseUri.Should().Be(new Uri(expected));
        result.Source.Should().Be(ApiEndpointSource.ConfigFile);
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void Base_address_always_ends_in_a_slash()
    {
        // Refit drops the last path segment of a base address without a trailing slash.
        WriteConfig("{\"serverUrl\": \"http://gate-pc:5000\"}");

        ApiEndpointResolver.Resolve(_dir, null).BaseUri.AbsoluteUri.Should().EndWith("/");
    }

    [Fact]
    public void Property_name_is_matched_case_insensitively()
    {
        WriteConfig("{\"ServerUrl\": \"192.168.1.10\"}");

        var result = ApiEndpointResolver.Resolve(_dir, environmentOverride: null);

        result.Source.Should().Be(ApiEndpointSource.ConfigFile);
        result.BaseUri.Should().Be(new Uri("http://192.168.1.10:5000/"));
    }

    [Fact]
    public void Utf8_bom_is_tolerated()
    {
        // Notepad on Windows 10 writes a BOM by default; System.Text.Json rejects one.
        File.WriteAllText(
            Path.Combine(_dir, ApiEndpointResolver.ConfigFileName),
            "{\"serverUrl\": \"192.168.1.10\"}",
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var result = ApiEndpointResolver.Resolve(_dir, environmentOverride: null);

        result.Source.Should().Be(ApiEndpointSource.ConfigFile);
        result.BaseUri.Should().Be(new Uri("http://192.168.1.10:5000/"));
    }

    // -- Bad input must be loud, not silent ---------------------------------------------

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://192.168.1.10")]
    [InlineData("")]
    public void Invalid_address_falls_back_and_warns(string configured)
    {
        WriteConfig("{\"serverUrl\": \"" + configured + "\"}");

        var result = ApiEndpointResolver.Resolve(_dir, environmentOverride: null);

        result.BaseUri.Should().Be(new Uri(ApiEndpointResolver.DefaultBaseAddress));
        result.Source.Should().Be(ApiEndpointSource.Default);
        if (configured.Length > 0)
        {
            result.Warning.Should().NotBeNull();
        }
    }

    [Fact]
    public void Malformed_json_falls_back_and_warns()
    {
        WriteConfig("{ this is not json");

        var result = ApiEndpointResolver.Resolve(_dir, environmentOverride: null);

        result.BaseUri.Should().Be(new Uri(ApiEndpointResolver.DefaultBaseAddress));
        result.Warning.Should().NotBeNull();
    }

    [Fact]
    public void Non_string_property_falls_back_and_warns()
    {
        WriteConfig("{\"serverUrl\": 5000}");

        var result = ApiEndpointResolver.Resolve(_dir, environmentOverride: null);

        result.BaseUri.Should().Be(new Uri(ApiEndpointResolver.DefaultBaseAddress));
        result.Warning.Should().NotBeNull();
    }

    // -- Precedence ----------------------------------------------------------------------

    [Fact]
    public void Environment_variable_wins_over_config_file()
    {
        WriteConfig("{\"serverUrl\": \"192.168.1.10\"}");

        var result = ApiEndpointResolver.Resolve(_dir, "10.0.0.5:6000");

        result.BaseUri.Should().Be(new Uri("http://10.0.0.5:6000/"));
        result.Source.Should().Be(ApiEndpointSource.Environment);
    }

    [Fact]
    public void Invalid_environment_variable_does_not_fall_through_to_the_config_file()
    {
        // Silently using a different server than the one the operator set would be worse
        // than refusing: they would chase a data problem that is really an address problem.
        WriteConfig("{\"serverUrl\": \"192.168.1.10\"}");

        var result = ApiEndpointResolver.Resolve(_dir, "not a url");

        result.BaseUri.Should().Be(new Uri(ApiEndpointResolver.DefaultBaseAddress));
        result.Source.Should().Be(ApiEndpointSource.Default);
        result.Warning.Should().Contain(ApiEndpointResolver.EnvironmentVariableName);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_dir, ApiEndpointResolver.ConfigFileName), json);
}
