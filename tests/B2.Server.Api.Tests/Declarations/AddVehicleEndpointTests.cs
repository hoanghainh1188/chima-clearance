using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Declarations;

public sealed class AddVehicleEndpointTests
{
    private static async Task<Guid> CreateDeclarationWithOneVehicleAsync(HttpClient client)
    {
        var no = TestDeclarationNumbers.Next();
        var res = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("29A10001", null, null)),
            TestJson.Options);
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        return dto!.Id;
    }

    [Fact]
    [Trait("Feature", "F-06")]
    public async Task Should_add_second_vehicle_to_declaration()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var declId = await CreateDeclarationWithOneVehicleAsync(client);
        var add = await client.PostAsJsonAsync(
            $"/api/declarations/{declId}/vehicles",
            new VehicleAddRequest("29A10002", null, null),
            TestJson.Options);
        add.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        (await db.Vehicles.CountAsync(v => v.DeclarationId == declId)).Should().Be(2);

        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.DeclarationId == declId).ToListAsync();
        var second = vehicles.Single(v => v.PlateNumber == "29A10002");
        (await AuditLogQuery.CountForEntityAsync(factory, "Vehicle", second.Id)).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Feature", "F-06")]
    public async Task Should_reject_add_vehicle_when_kvgs_confirmed()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var declId = await CreateDeclarationWithOneVehicleAsync(client);
        await DeclarationTestMutations.SetStatusKvgsConfirmedAsync(factory, declId, SeedIds.AdminUserId);
        var add = await client.PostAsJsonAsync(
            $"/api/declarations/{declId}/vehicles",
            new VehicleAddRequest("29A10003", null, null),
            TestJson.Options);
        add.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = JsonDocument.Parse(await add.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be("DECLARATION_LOCKED");
    }

    [Fact]
    [Trait("Feature", "F-06")]
    public async Task Should_reject_add_vehicle_when_cancelled()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var declId = await CreateDeclarationWithOneVehicleAsync(client);
        await DeclarationTestMutations.SetStatusCancelledAsync(factory, declId, SeedIds.AdminUserId);
        var add = await client.PostAsJsonAsync(
            $"/api/declarations/{declId}/vehicles",
            new VehicleAddRequest("29A10004", null, null),
            TestJson.Options);
        add.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = JsonDocument.Parse(await add.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be("DECLARATION_LOCKED");
    }
}
