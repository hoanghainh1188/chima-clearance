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

    [Fact]
    [Trait("Feature", "F-04")]
    public async Task Listing_without_includeInactive_returns_active_rows_not_500()
    {
        // The parameter was non-nullable with no default, so omitting it threw
        // BadHttpRequestException and the global handler turned it into a 500 INTERNAL_ERROR.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);

        var response = await client.GetAsync("/api/master/yards");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<YardListResponse>();
        page!.Items.Should().OnlyContain(y => y.IsActive);
    }

    [Fact]
    [Trait("Feature", "F-04")]
    public async Task Listing_processors_without_includeInactive_returns_200()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);

        var response = await client.GetAsync("/api/master/processors");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
