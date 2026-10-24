using B2.Infrastructure.Auth;
using FluentAssertions;

namespace B2.Server.Api.Tests.Auth;

public sealed class RsaJwtKeyProviderTests
{
    [Fact]
    [Trait("Issue", "148")]
    public async Task Concurrent_cold_start_callers_get_one_signing_key()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-jwt-{Guid.NewGuid():N}");
        try
        {
            var provider = new RsaJwtKeyProvider(dir);
            using var start = new ManualResetEventSlim();

            var callers = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                start.Wait();
                return provider.GetSigningKey();
            })).ToArray();
            start.Set();
            var keys = await Task.WhenAll(callers);

            keys.Distinct().Should().ContainSingle("a second key pair would fail validation of tokens signed by the first");
            Directory.GetFiles(Path.Combine(dir, "keys"), "*.tmp").Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
