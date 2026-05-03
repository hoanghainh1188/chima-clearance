using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.MasterData;
using FluentAssertions;

namespace B2.Server.Api.Tests.MasterData;

public sealed class YardsEndpointTests
{
    [Fact]
    [Trait("Feature", "F-04")]
    public async Task Yard_code_uniqueness_second_insert_with_same_code_returns_409()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var first = await client.PostAsJsonAsync(
            "/api/master/yards",
            new YardWriteRequest("Z1", "Yard One", null));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await client.PostAsJsonAsync(
            "/api/master/yards",
            new YardWriteRequest("Z1", "Yard Dup", null));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
