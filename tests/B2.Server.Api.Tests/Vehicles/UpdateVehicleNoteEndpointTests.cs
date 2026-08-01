using System.Net;
using System.Net.Http.Json;
using B2.Domain.Declarations;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;

namespace B2.Server.Api.Tests.Vehicles;

/// <summary>
/// Business rules § 3 lists "Ghi chú" (cột O) among the five columns the exit-gate officer may
/// fill, so the note must stay editable after entry — an officer who spots a problem at the lane
/// (xe hỏng, hàng có vấn đề) has to be able to record it.
/// </summary>
public sealed class UpdateVehicleNoteEndpointTests
{
    private static async Task<(Guid DeclarationId, Guid VehicleId)> CreateDeclarationAsync(HttpClient client, string plate)
    {
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "Công ty ABC",
                "Hàng hóa",
                SeedIds.YardId,
                new VehicleAddRequest(plate, null, "ghi chú lúc nhập")),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        var detail = await client.GetFromJsonAsync<DeclarationDetailResponse>(
            $"/api/declarations/{dto.Id}",
            TestJson.Options);
        return (dto.Id, detail!.Vehicles[0].Id);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Exit_gate_operator_can_edit_the_note()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (declId, vehicleId) = await CreateDeclarationAsync(client, "30A51001");

        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        var res = await client.PatchAsJsonAsync(
            $"/api/vehicles/{vehicleId}/note",
            new UpdateVehicleNoteRequest("Xe hỏng phanh, chờ xử lý"),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await client.GetFromJsonAsync<DeclarationDetailResponse>(
            $"/api/declarations/{declId}",
            TestJson.Options);
        detail!.Vehicles[0].Note.Should().Be("Xe hỏng phanh, chờ xử lý");

        // FR-003: the edit is on the append-only audit trail.
        (await AuditLogQuery.CountForEntityAsync(factory, nameof(Vehicle), vehicleId))
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Blank_note_clears_it()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (declId, vehicleId) = await CreateDeclarationAsync(client, "30A51002");

        var res = await client.PatchAsJsonAsync(
            $"/api/vehicles/{vehicleId}/note",
            new UpdateVehicleNoteRequest("   "),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var detail = await client.GetFromJsonAsync<DeclarationDetailResponse>(
            $"/api/declarations/{declId}",
            TestJson.Options);
        detail!.Vehicles[0].Note.Should().BeNull();
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Note_on_a_cancelled_declaration_is_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (declId, vehicleId) = await CreateDeclarationAsync(client, "30A51003");

        var cancel = await client.PostAsJsonAsync(
            $"/api/declarations/{declId}/cancel",
            new CancelDeclarationRequest("Nhập nhầm"),
            TestJson.Options);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var res = await client.PatchAsJsonAsync(
            $"/api/vehicles/{vehicleId}/note",
            new UpdateVehicleNoteRequest("Không được ghi"),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Note_longer_than_500_characters_is_rejected()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (_, vehicleId) = await CreateDeclarationAsync(client, "30A51004");

        var res = await client.PatchAsJsonAsync(
            $"/api/vehicles/{vehicleId}/note",
            new UpdateVehicleNoteRequest(new string('x', 501)),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Unknown_vehicle_returns_404()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var res = await client.PatchAsJsonAsync(
            $"/api/vehicles/{Guid.NewGuid()}/note",
            new UpdateVehicleNoteRequest("x"),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
