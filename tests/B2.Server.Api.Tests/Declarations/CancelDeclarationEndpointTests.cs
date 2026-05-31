using System.Net;
using System.Net.Http.Json;
using B2.Domain.Audit;
using B2.Domain.Declarations;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Declarations;

public sealed class CancelDeclarationEndpointTests
{
    private static HttpRequestMessage ExitRequest(Guid vehicleId, string idemKey)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vehicleId}/exit");
        msg.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
        return msg;
    }

    private static async Task<DeclarationDto> CreateOpenDeclarationAsync(HttpClient client, string plate)
    {
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                TestDeclarationNumbers.Next(),
                SeedIds.ProcessorId,
                "Công ty ABC",
                "Hàng hóa",
                SeedIds.YardId,
                new VehicleAddRequest(plate, null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_admin_cancel_open_declaration_with_reason()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40001");

        var res = await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("Nhập nhầm tờ khai"),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var entity = await db.Declarations.AsNoTracking().SingleAsync(d => d.Id == decl.Id);
        entity.Status.Should().Be(DeclarationStatus.Cancelled);
        entity.CancellationReason.Should().Be("Nhập nhầm tờ khai");
        entity.CancelledByUserId.Should().Be(admin.UserId);

        var audit = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == nameof(Declaration) && a.EntityId == decl.Id && a.Action == AuditAction.Cancel)
            .SingleAsync();
        audit.Reason.Should().Be("Nhập nhầm tờ khai");
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_reject_cancel_without_reason()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40002");

        var res = await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("   "),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [Trait("Requirement", "FR-022")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_forbid_non_admin_from_cancelling(bool supervisor)
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, supervisor ? "31A40003" : "31A40004");

        client.DefaultRequestHeaders.Remove("Authorization");
        var actor = supervisor
            ? await client.LoginAsNewSupervisorAsync()
            : await client.LoginAsNewOperatorAsync();
        client.SetBearer(actor.AccessToken);

        var res = await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("Không được phép"),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_reject_cancel_when_not_open()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40005");

        Guid vehicleId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking().Where(v => v.DeclarationId == decl.Id).Select(v => v.Id).SingleAsync();
        }

        // Exit the only vehicle → declaration becomes AllExited, no longer cancellable.
        (await client.SendAsync(ExitRequest(vehicleId, Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        var res = await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("Quá muộn"),
            TestJson.Options);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_prevent_exit_after_cancel()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40006");

        Guid vehicleId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking().Where(v => v.DeclarationId == decl.Id).Select(v => v.Id).SingleAsync();
        }

        (await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("Hủy trước khi ra"),
            TestJson.Options)).EnsureSuccessStatusCode();

        var exit = await client.SendAsync(ExitRequest(vehicleId, Guid.NewGuid().ToString("N")));
        exit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_prevent_add_vehicle_after_cancel()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40007");

        (await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("Hủy"),
            TestJson.Options)).EnsureSuccessStatusCode();

        var add = await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/vehicles",
            new VehicleAddRequest("31A40008", null, null),
            TestJson.Options);
        add.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
