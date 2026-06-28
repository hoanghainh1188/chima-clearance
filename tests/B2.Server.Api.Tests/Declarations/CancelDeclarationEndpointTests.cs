using System.Net;
using System.Net.Http.Json;
using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Users;
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
    public async Task Should_prevent_un_exit_after_cancel()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40008");

        (await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/vehicles",
            new VehicleAddRequest("31A40009", null, null),
            TestJson.Options)).EnsureSuccessStatusCode();

        Guid exitedVehicleId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            exitedVehicleId = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == decl.Id && v.PlateNumber == "31A40008")
                .Select(v => v.Id)
                .SingleAsync();
        }

        // Exit one of two vehicles — declaration stays Open so cancel remains valid (INV-9).
        (await client.SendAsync(ExitRequest(exitedVehicleId, Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/cancel",
            new CancelDeclarationRequest("Hủy khi vẫn còn xe chưa ra"),
            TestJson.Options)).EnsureSuccessStatusCode();

        var unExit = await client.PostAsJsonAsync(
            $"/api/vehicles/{exitedVehicleId}/un-exit",
            new UnExitVehicleRequest("Không được sửa tờ đã hủy"),
            TestJson.Options);
        unExit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_not_cancel_when_concurrent_exit_marks_vehicle()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40008");

        Guid vehicleId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == decl.Id)
                .Select(v => v.Id)
                .SingleAsync();
        }

        var exitTask = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDeclarationRepository>();
            try
            {
                await repo.MarkVehicleExitAsync(vehicleId, admin.UserId, DateTimeOffset.UtcNow, CancellationToken.None);
            }
            catch (DomainException)
            {
                // Expected when cancel wins the Serializable race first.
            }
        });

        var cancelTask = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDeclarationRepository>();
            try
            {
                await repo.CancelDeclarationAsync(
                    decl.Id,
                    Role.Admin,
                    admin.UserId,
                    "Đua tranh với xuất cổng",
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
            }
            catch (DomainException)
            {
                // Expected when exit wins the Serializable race first.
            }
        });

        await Task.WhenAll(exitTask, cancelTask);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var entity = await db.Declarations.AsNoTracking().SingleAsync(d => d.Id == decl.Id);
            var exited = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == decl.Id)
                .AnyAsync(v => v.ExitMarked);

            if (exited)
            {
                entity.Status.Should().Be(
                    DeclarationStatus.AllExited,
                    "a concurrent exit must not be overwritten by cancel (INV-9)");
            }
        }
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_not_undo_cancel_when_concurrent_un_exit_runs()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40008");

        (await client.PostAsJsonAsync(
            $"/api/declarations/{decl.Id}/vehicles",
            new VehicleAddRequest("31A40009", null, null),
            TestJson.Options)).EnsureSuccessStatusCode();

        Guid exitedVehicleId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            exitedVehicleId = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == decl.Id && v.PlateNumber == "31A40008")
                .Select(v => v.Id)
                .SingleAsync();
        }

        (await client.SendAsync(ExitRequest(exitedVehicleId, Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        var unExitTask = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDeclarationRepository>();
            try
            {
                await repo.UnExitVehicleAsync(exitedVehicleId, CancellationToken.None);
            }
            catch (DomainException)
            {
                // Expected when cancel wins the Serializable race first.
            }
        });

        var cancelTask = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IDeclarationRepository>();
            try
            {
                await repo.CancelDeclarationAsync(
                    decl.Id,
                    Role.Admin,
                    admin.UserId,
                    "Đua tranh với un-exit",
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
            }
            catch (DomainException)
            {
                // Expected when un-exit wins the Serializable race first.
            }
        });

        await Task.WhenAll(unExitTask, cancelTask);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var entity = await db.Declarations.AsNoTracking().SingleAsync(d => d.Id == decl.Id);

            if (entity.Status == DeclarationStatus.Cancelled)
            {
                var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == exitedVehicleId);
                vehicle.ExitMarked.Should().BeTrue(
                    "cancel must not be undone by a concurrent un-exit (FR-022)");
            }
        }
    }

    [Fact]
    [Trait("Requirement", "FR-022")]
    public async Task Should_prevent_add_vehicle_after_cancel()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var decl = await CreateOpenDeclarationAsync(client, "31A40010");

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
