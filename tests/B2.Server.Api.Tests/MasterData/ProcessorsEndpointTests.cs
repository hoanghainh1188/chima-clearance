using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.MasterData;
using FluentAssertions;

namespace B2.Server.Api.Tests.MasterData;

public sealed class ProcessorsEndpointTests
{
    [Fact]
    [Trait("Feature", "F-03")]
    public async Task Add_processor_happy_path_returns_201()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var res = await client.PostAsJsonAsync(
            "/api/master/processors",
            new ProcessorWriteRequest("Nguyen Van Nam Test", null));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Feature", "F-03")]
    public async Task Reject_unknown_processor_on_declaration_entry_returns_400()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var unknownProcessor = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var req = new DeclarationCreateRequest(
            "123456789012",
            unknownProcessor,
            "Cong Ty A",
            "Hang Hoa B",
            SeedIds.YardId,
            new VehicleAddRequest("29A12345", null, null));
        var res = await client.PostAsJsonAsync("/api/declarations", req);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
