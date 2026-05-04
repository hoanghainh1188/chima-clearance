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

public sealed class CreateDeclarationEndpointTests
{
    [Fact]
    [Trait("Feature", "F-05")]
    public async Task Should_happy_path_entry_create_declaration_and_uppercase_plate()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var before = DateTimeOffset.UtcNow;
        var res = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                "123456789012",
                SeedIds.ProcessorId,
                "Cty TNHH ABC",
                "Hang hoa",
                SeedIds.YardId,
                new VehicleAddRequest("12h05769", null, null)),
            TestJson.Options);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        body!.DeclarationNumber.Should().Be("123456789012");

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(v => v.DeclarationId == body.Id);
        vehicle.PlateNumber.Should().Be("12H05769");
        vehicle.EntryTime.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow.AddMinutes(1));
        vehicle.EnteredByUserId.Should().Be(SeedIds.AdminUserId);

        (await AuditLogQuery.CountForEntityAsync(factory, "Declaration", body.Id))
            .Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Feature", "F-05")]
    public async Task Should_duplicate_declaration_number_returns_409()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var req = new DeclarationCreateRequest(
            "999999999999",
            SeedIds.ProcessorId,
            "C1",
            "G1",
            SeedIds.YardId,
            new VehicleAddRequest("29A12345", null, null));
        (await client.PostAsJsonAsync("/api/declarations", req, TestJson.Options)).EnsureSuccessStatusCode();
        var dup = await client.PostAsJsonAsync("/api/declarations", req, TestJson.Options);
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = JsonDocument.Parse(await dup.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be("DECLARATION_NUMBER_DUPLICATE");
    }

    [Fact]
    [Trait("Feature", "F-05")]
    public async Task Should_12_digit_validation_returns_400()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var res = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                "12345",
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("29A12345", null, null)),
            TestJson.Options);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Feature", "F-05")]
    public async Task Should_idempotent_retry_same_idempotency_key_replay_success_without_second_audit_row()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var key = "idem-" + Guid.NewGuid().ToString("N");
        var req = new DeclarationCreateRequest(
            "111111111111",
            SeedIds.ProcessorId,
            "C1",
            "G1",
            SeedIds.YardId,
            new VehicleAddRequest("29A12345", null, null));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Idempotency-Key", key);
        var first = await client.PostAsJsonAsync("/api/declarations", req, TestJson.Options);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstDto = await first.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        var second = await client.PostAsJsonAsync("/api/declarations", req, TestJson.Options);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        var secondDto = await second.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        secondDto!.Id.Should().Be(firstDto!.Id);

        (await AuditLogQuery.CountForEntityAsync(factory, "Declaration", firstDto.Id))
            .Should().Be(1);
    }
}
