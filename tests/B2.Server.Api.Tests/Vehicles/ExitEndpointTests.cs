using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Vehicles;

public sealed class ExitEndpointTests
{
    private static async Task<(Guid DeclarationId, Guid V1, Guid V2, Guid V3)> CreateThreeVehicleDeclarationAsync(
        HttpClient client,
        B2ApiFactory factory)
    {
        var no = TestDeclarationNumbers.Next();
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("30A11111", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var dto = await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        var declId = dto!.Id;
        await client.PostAsJsonAsync(
            $"/api/declarations/{declId}/vehicles",
            new VehicleAddRequest("30A22222", null, null),
            TestJson.Options);
        await client.PostAsJsonAsync(
            $"/api/declarations/{declId}/vehicles",
            new VehicleAddRequest("30A33333", null, null),
            TestJson.Options);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var ids = await db.Vehicles.AsNoTracking()
            .Where(v => v.DeclarationId == declId)
            .OrderBy(v => v.PlateNumber)
            .Select(v => v.Id)
            .ToListAsync();
        return (declId, ids[0], ids[1], ids[2]);
    }

    private static HttpRequestMessage ExitRequest(Guid vehicleId, string idemKey)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vehicleId}/exit");
        msg.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
        return msg;
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Should_mark_exit_and_set_exit_fields()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (_, v1, _, _) = await CreateThreeVehicleDeclarationAsync(client, factory);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        var res = await client.SendAsync(ExitRequest(v1, Guid.NewGuid().ToString("N")));
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<VehicleExitResponse>(TestJson.Options);
        body!.VehicleId.Should().Be(v1);
        body.ExitTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var v = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == v1);
        v.ExitMarked.Should().BeTrue();
        v.ExitConfirmedByUserId.Should().NotBeNull();

        (await AuditLogQuery.CountForEntityAsync(factory, "Vehicle", v1)).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Should_auto_promote_declaration_to_all_exited_when_last_vehicle_exits()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (declId, v1, v2, v3) = await CreateThreeVehicleDeclarationAsync(client, factory);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        await client.SendAsync(ExitRequest(v1, Guid.NewGuid().ToString("N")));
        await client.SendAsync(ExitRequest(v2, Guid.NewGuid().ToString("N")));
        var last = await client.SendAsync(ExitRequest(v3, Guid.NewGuid().ToString("N")));
        last.EnsureSuccessStatusCode();
        var exitBody = await last.Content.ReadFromJsonAsync<VehicleExitResponse>(TestJson.Options);
        exitBody!.DeclarationStatus.Should().Be(nameof(DeclarationStatus.AllExited));

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var status = await db.Declarations.AsNoTracking().Where(d => d.Id == declId).Select(d => d.Status).SingleAsync();
        status.Should().Be(DeclarationStatus.AllExited);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Should_idempotent_exit_replay_same_idempotency_key_without_second_audit()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (_, v1, _, _) = await CreateThreeVehicleDeclarationAsync(client, factory);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        var key = "exit-idem-" + Guid.NewGuid().ToString("N");
        var first = await client.SendAsync(ExitRequest(v1, key));
        first.EnsureSuccessStatusCode();

        var beforeAudit = await AuditLogQuery.CountForEntityAsync(factory, "Vehicle", v1);
        var second = await client.SendAsync(ExitRequest(v1, key));
        second.EnsureSuccessStatusCode();
        (await AuditLogQuery.CountForEntityAsync(factory, "Vehicle", v1)).Should().Be(beforeAudit);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Should_operator_receive_409_when_marking_exit_twice_with_different_idempotency_keys()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (_, v1, _, _) = await CreateThreeVehicleDeclarationAsync(client, factory);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        await client.SendAsync(ExitRequest(v1, Guid.NewGuid().ToString("N")));
        var dup = await client.SendAsync(ExitRequest(v1, Guid.NewGuid().ToString("N")));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = JsonDocument.Parse(await dup.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be("VEHICLE_ALREADY_EXITED");
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Should_operator_be_forbidden_from_un_exit()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (_, v1, _, _) = await CreateThreeVehicleDeclarationAsync(client, factory);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        await client.SendAsync(ExitRequest(v1, Guid.NewGuid().ToString("N")));

        var un = await client.PostAsJsonAsync(
            $"/api/vehicles/{v1}/un-exit",
            new UnExitVehicleRequest("wrong"),
            TestJson.Options);
        un.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Should_admin_un_exit_override_and_write_audit_reason()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (_, v1, _, _) = await CreateThreeVehicleDeclarationAsync(client, factory);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        await client.SendAsync(ExitRequest(v1, Guid.NewGuid().ToString("N")));

        client.DefaultRequestHeaders.Remove("Authorization");
        var admin2 = await client.LoginAsAdminAsync();
        client.SetBearer(admin2.AccessToken);
        var un = await client.PostAsJsonAsync(
            $"/api/vehicles/{v1}/un-exit",
            new UnExitVehicleRequest("wrong vehicle"),
            TestJson.Options);
        un.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var v = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == v1);
        v.ExitMarked.Should().BeFalse();

        var audit = (await db.AuditLogs.AsNoTracking()
                .Where(a => a.EntityType == "Vehicle" && a.EntityId == v1 && a.Action == AuditAction.UnExit)
                .ToListAsync())
            .OrderByDescending(a => a.At)
            .First();
        audit.Reason.Should().Be("wrong vehicle");
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_prevent_un_exit_after_kvgs_confirm()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var no = TestDeclarationNumbers.Next();
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("30A99991", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var decl = await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        Guid vehicleId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == decl!.Id)
                .Select(v => v.Id)
                .SingleAsync();
        }

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        (await client.SendAsync(ExitRequest(vehicleId, Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Remove("Authorization");
        var sup = await client.LoginAsNewSupervisorAsync();
        client.SetBearer(sup.AccessToken);
        (await client.PostAsync($"/api/declarations/{decl!.Id}/kvgs-confirm", null)).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Remove("Authorization");
        var admin2 = await client.LoginAsAdminAsync();
        client.SetBearer(admin2.AccessToken);
        var unExit = await client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/un-exit",
            new UnExitVehicleRequest("Không được sửa tờ đã KVGS"),
            TestJson.Options);
        unExit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Feature", "F-08")]
    public async Task Should_fr024_first_exit_wins_second_gets_conflict_payload()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var (_, v1, _, _) = await CreateThreeVehicleDeclarationAsync(client, factory);

        client.DefaultRequestHeaders.Remove("Authorization");
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);

        var c1 = factory.CreateClient();
        c1.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", op.AccessToken);
        var c2 = factory.CreateClient();
        c2.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", op.AccessToken);

        var t1 = c1.SendAsync(ExitRequest(v1, "k-" + Guid.NewGuid().ToString("N")));
        var t2 = c2.SendAsync(ExitRequest(v1, "k-" + Guid.NewGuid().ToString("N")));
        await Task.WhenAll(t1, t2);

        var r1 = await t1;
        var r2 = await t2;
        var ok = new[] { r1, r2 }.Single(x => x.IsSuccessStatusCode);
        var fail = new[] { r1, r2 }.Single(x => !x.IsSuccessStatusCode);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        fail.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = JsonDocument.Parse(await fail.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("code").GetString().Should().Be("VEHICLE_ALREADY_EXITED");
        json.RootElement.GetProperty("winnerUsername").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("exitTime").GetString().Should().NotBeNullOrEmpty();
    }
}
