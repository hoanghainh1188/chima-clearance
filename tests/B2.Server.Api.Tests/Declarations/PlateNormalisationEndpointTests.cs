using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;

namespace B2.Server.Api.Tests.Declarations;

/// <summary>
/// Cột E/F are pasted from the unit's internal system, so a plate or trailer number carrying a
/// dash must be accepted and stored in the canonical compact form — not rejected at the gate.
/// </summary>
public sealed class PlateNormalisationEndpointTests
{
    [Fact]
    [Trait("Feature", "F-05")]
    public async Task Plate_and_trailer_with_separators_are_accepted_and_stored_normalised()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "Công ty ABC",
                "Hàng hóa",
                SeedIds.YardId,
                new VehicleAddRequest("29c-12345", "51rm-09876", null)),
            TestJson.Options);

        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var detail = await client.GetFromJsonAsync<DeclarationDetailResponse>(
            $"/api/declarations/{dto.Id}",
            TestJson.Options);

        detail!.Vehicles[0].PlateNumber.Should().Be("29C12345");
        detail.Vehicles[0].TrailerOrContainerNumber.Should().Be("51RM09876");
    }

    [Fact]
    [Trait("Feature", "F-07")]
    public async Task Suffix_search_finds_a_vehicle_entered_with_a_dash()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "Công ty ABC",
                "Hàng hóa",
                SeedIds.YardId,
                new VehicleAddRequest("29c-54321", null, null)),
            TestJson.Options);
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var results = await client.GetFromJsonAsync<List<VehicleSearchResultDto>>(
            "/api/vehicles/search?suffix=4321",
            TestJson.Options);

        results.Should().ContainSingle(r => r.PlateNumber == "29C54321");
    }

    [Fact]
    [Trait("Feature", "F-05")]
    public async Task A_plate_that_is_still_invalid_after_normalising_is_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "Công ty ABC",
                "Hàng hóa",
                SeedIds.YardId,
                new VehicleAddRequest("29-123", null, null)),
            TestJson.Options);

        create.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
