using System.Net;
using System.Net.Http.Json;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Vehicles;

public sealed class SuggestEndpointTests
{
    // Seed: EntryTime used to fall within / outside the 90-day window.
    private static DateTimeOffset Recent => DateTimeOffset.UtcNow.AddDays(-30);
    private static DateTimeOffset TooOld => DateTimeOffset.UtcNow.AddDays(-91);

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_suggest_trailer_for_known_plate()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        // Pair "12H05769" → "12RM05769" 28 times
        for (var i = 0; i < 28; i++)
        {
            var decl = TestDeclarationNumbers.Next();
            await client.PostAsJsonAsync(
                "/api/declarations",
                new DeclarationCreateRequest([decl], SeedIds.ProcessorId, "Company A", "Goods A",
                    SeedIds.YardId, new VehicleAddRequest("12H05769", "12RM05769", null)),
                TestJson.Options);
        }

        var response = await client.GetAsync("/api/vehicles/suggest?plate=12H05769&field=trailer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSuggestResponse>(TestJson.Options);
        body.Should().NotBeNull();
        body!.Value.Should().Be("12RM05769");
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_return_null_value_for_unknown_plate()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var response = await client.GetAsync("/api/vehicles/suggest?plate=99X99999&field=trailer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSuggestResponse>(TestJson.Options);
        body!.Value.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_ignore_pairings_older_than_90_days()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        // Seed an old pairing directly in the DB — bypasses the API which always uses UtcNow.
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var decl = Domain.Declarations.Declaration.CreateNew(
                Guid.NewGuid(),
                [Domain.Declarations.DeclarationNumber.Parse("999999999999")],
                SeedIds.ProcessorId, "OldCo", "OldGoods", SeedIds.YardId,
                SeedIds.AdminUserId, TooOld, DateOnly.FromDateTime(TooOld.UtcDateTime));
            decl.AddVehicle(Guid.NewGuid(), Domain.Declarations.PlateNumber.Parse("44A99999"),
                TooOld, SeedIds.AdminUserId, Domain.Declarations.ContainerNumber.TryParse("OLDTRAILER"), null);
            db.Declarations.Add(decl);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/vehicles/suggest?plate=44A99999&field=trailer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSuggestResponse>(TestJson.Options);
        body!.Value.Should().BeNull();
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_tiebreak_by_most_recent_occurrence()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        // 1 occurrence of TRAILERA and 1 occurrence of TRAILERB seeded via API (both recent).
        // TRAILERA seeded with an older EntryTime directly so TRAILERB wins on recency.
        // Fixtures carry no dash: cột F is separator-stripped, so a dash would not survive.
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();

            var old = DateTimeOffset.UtcNow.AddDays(-10);
            var declA = Domain.Declarations.Declaration.CreateNew(
                Guid.NewGuid(), [Domain.Declarations.DeclarationNumber.Parse("111111111111")],
                SeedIds.ProcessorId, "Co", "G", SeedIds.YardId, SeedIds.AdminUserId, old,
                DateOnly.FromDateTime(old.UtcDateTime));
            declA.AddVehicle(Guid.NewGuid(), Domain.Declarations.PlateNumber.Parse("55B12345"),
                old, SeedIds.AdminUserId, Domain.Declarations.ContainerNumber.TryParse("TRAILERA"), null);
            db.Declarations.Add(declA);

            var newer = DateTimeOffset.UtcNow.AddDays(-2);
            var declB = Domain.Declarations.Declaration.CreateNew(
                Guid.NewGuid(), [Domain.Declarations.DeclarationNumber.Parse("222222222222")],
                SeedIds.ProcessorId, "Co", "G", SeedIds.YardId, SeedIds.AdminUserId, newer,
                DateOnly.FromDateTime(newer.UtcDateTime));
            declB.AddVehicle(Guid.NewGuid(), Domain.Declarations.PlateNumber.Parse("55B12345"),
                newer, SeedIds.AdminUserId, Domain.Declarations.ContainerNumber.TryParse("TRAILERB"), null);
            db.Declarations.Add(declB);

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/vehicles/suggest?plate=55B12345&field=trailer");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSuggestResponse>(TestJson.Options);
        body!.Value.Should().Be("TRAILERB");
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_suggest_company_name()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()], SeedIds.ProcessorId,
                "Công ty ABC", "Hàng hoá X", SeedIds.YardId,
                new VehicleAddRequest("77C55555", null, null)),
            TestJson.Options);

        var response = await client.GetAsync("/api/vehicles/suggest?plate=77C55555&field=company");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSuggestResponse>(TestJson.Options);
        body!.Value.Should().Be("Công ty ABC");
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_suggest_goods_name()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()], SeedIds.ProcessorId,
                "Công ty XYZ", "Hàng hoá đặc biệt", SeedIds.YardId,
                new VehicleAddRequest("88D66666", null, null)),
            TestJson.Options);

        var response = await client.GetAsync("/api/vehicles/suggest?plate=88D66666&field=goods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VehicleSuggestResponse>(TestJson.Options);
        body!.Value.Should().Be("Hàng hoá đặc biệt");
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_return_400_for_invalid_field()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var response = await client.GetAsync("/api/vehicles/suggest?plate=12H05769&field=unknown");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_return_401_when_unauthenticated()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles/suggest?plate=12H05769&field=trailer");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Feature", "F-17")]
    public async Task Should_return_400_when_plate_is_missing()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var response = await client.GetAsync("/api/vehicles/suggest?field=trailer");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
