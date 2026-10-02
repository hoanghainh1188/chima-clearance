using System.Net;
using System.Net.Http.Json;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Vehicles;

public sealed class SearchEndpointTests
{
    [Fact]
    [Trait("Feature", "F-07")]
    public async Task Should_search_return_only_unexited_vehicles_by_default()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl1 = TestDeclarationNumbers.Next();
        var decl2 = TestDeclarationNumbers.Next();
        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [decl1],
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("12H05769", null, null)),
            TestJson.Options);
        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [decl2],
                SeedIds.ProcessorId,
                "C2",
                "G2",
                SeedIds.YardId,
                new VehicleAddRequest("11H05769", null, null)),
            TestJson.Options);

        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var exited = await db.Vehicles
                .Include(v => v.Declaration)
                .ThenInclude(d => d!.Vehicles)
                .FirstAsync(v => v.PlateNumber == "11H05769");
            exited.MarkExit(SeedIds.AdminUserId, DateTimeOffset.UtcNow);
            exited.Declaration!.RecomputeStatus();
            await db.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        var search = await client.GetAsync("/api/vehicles/search?suffix=5769&unexited=true");
        search.EnsureSuccessStatusCode();
        var list = await search.Content.ReadFromJsonAsync<List<VehicleSearchResultDto>>(TestJson.Options);
        list!.Should().ContainSingle(v => v.PlateNumber == "12H05769");
        list.Should().NotContain(v => v.PlateNumber == "11H05769");
    }

    [Fact]
    [Trait("Feature", "F-07")]
    [Trait("Requirement", "FR-023")]
    public async Task Search_excludes_vehicles_of_cancelled_declarations()
    {
        // Audit S1: a cancelled declaration's vehicle used to show up at the exit gate and then
        // fail with 409 when marked exited.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("29C18642", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var decl = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
        (await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel", new CancelDeclarationRequest("Nhập nhầm"), TestJson.Options))
            .EnsureSuccessStatusCode();

        var search = await client.GetAsync("/api/vehicles/search?suffix=8642&unexited=true");
        search.EnsureSuccessStatusCode();
        var list = await search.Content.ReadFromJsonAsync<List<VehicleSearchResultDto>>(TestJson.Options);

        list.Should().BeEmpty();
    }

    [Fact]
    [Trait("Feature", "F-07")]
    public async Task Should_search_three_or_four_digit_suffix()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = TestDeclarationNumbers.Next();
        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [decl],
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("29A12345", null, null)),
            TestJson.Options);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        (await client.GetFromJsonAsync<List<VehicleSearchResultDto>>("/api/vehicles/search?suffix=2345&unexited=true", TestJson.Options))!
            .Should().ContainSingle();
        (await client.GetFromJsonAsync<List<VehicleSearchResultDto>>("/api/vehicles/search?suffix=345&unexited=true", TestJson.Options))!
            .Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "F-07")]
    public async Task Should_list_all_unexited_vehicles_when_suffix_is_empty()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("51A11111", null, null)),
            TestJson.Options);
        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "C2",
                "G2",
                SeedIds.YardId,
                new VehicleAddRequest("52B22222", null, null)),
            TestJson.Options);

        // Empty suffix → all unexited vehicles (no 400), unlike the 3-4 digit path.
        var search = await client.GetAsync("/api/vehicles/search?suffix=&unexited=true");
        search.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await search.Content.ReadFromJsonAsync<List<VehicleSearchResultDto>>(TestJson.Options))!;
        list.Should().Contain(v => v.PlateNumber == "51A11111");
        list.Should().Contain(v => v.PlateNumber == "52B22222");
    }

    [Fact]
    [Trait("Feature", "F-07")]
    public async Task Should_search_multiple_matches_ordered_by_entry_time_ascending()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var d1 = TestDeclarationNumbers.Next();
        var d2 = TestDeclarationNumbers.Next();
        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [d1],
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("51A15769", null, null)),
            TestJson.Options);
        await Task.Delay(20);
        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [d2],
                SeedIds.ProcessorId,
                "C2",
                "G2",
                SeedIds.YardId,
                new VehicleAddRequest("52B15769", null, null)),
            TestJson.Options);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        var list = await client.GetFromJsonAsync<List<VehicleSearchResultDto>>(
            "/api/vehicles/search?suffix=5769&unexited=true",
            TestJson.Options);
        list!.Should().HaveCount(2);
        list[0].EntryTime.Should().BeBefore(list[1].EntryTime);
    }

    [Fact(Skip = "50k-row perf budget: execute on spec VM (quickstart smoke / NBomber).")]
    [Trait("Feature", "F-07")]
    public void Should_search_performance_placeholder_for_spec_vm() => Assert.True(true);

    [Fact]
    [Trait("Feature", "F-07")]
    public async Task Should_supervisor_be_forbidden_from_exit_endpoint()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = TestDeclarationNumbers.Next();
        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [decl],
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("77C15769", null, null)),
            TestJson.Options);
        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var vid = await db.Vehicles.AsNoTracking().Where(v => v.PlateNumber == "77C15769").Select(v => v.Id).SingleAsync();

        client.DefaultRequestHeaders.Remove("Authorization");
        var sup = await client.LoginAsNewSupervisorAsync();
        client.SetBearer(sup.AccessToken);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vid}/exit");
        req.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var res = await client.SendAsync(req);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
