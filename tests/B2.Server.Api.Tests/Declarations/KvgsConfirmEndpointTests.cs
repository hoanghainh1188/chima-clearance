using System.Net;
using System.Net.Http.Json;
using B2.Domain.Declarations;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Declarations;

public sealed class KvgsConfirmEndpointTests
{
    private static HttpRequestMessage ExitRequest(Guid vehicleId, string idemKey)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vehicleId}/exit");
        msg.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
        return msg;
    }

    private static async Task<(Guid DeclarationId, HttpClient Client)> CreateAllExitedWithClientAsync(B2ApiFactory factory)
    {
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
                new VehicleAddRequest("30A88881", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var dto = await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        var declId = dto!.Id;

        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var vid = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == declId)
                .Select(v => v.Id)
                .SingleAsync();
            client.DefaultRequestHeaders.Remove("Authorization");
            var op = await client.LoginAsNewOperatorAsync();
            client.SetBearer(op.AccessToken);
            (await client.SendAsync(ExitRequest(vid, Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();
        }

        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var status = await db.Declarations.AsNoTracking()
                .Where(d => d.Id == declId)
                .Select(d => d.Status)
                .SingleAsync();
            status.Should().Be(DeclarationStatus.AllExited);
        }

        return (declId, client);
    }

    [Fact]
    [Trait("Feature", "F-09")]
    public async Task Should_confirm_kvgs_and_set_fields()
    {
        await using var factory = new B2ApiFactory();
        var (declId, client) = await CreateAllExitedWithClientAsync(factory);
        client.DefaultRequestHeaders.Remove("Authorization");
        var sup = await client.LoginAsNewSupervisorAsync();
        client.SetBearer(sup.AccessToken);
        var before = DateTimeOffset.UtcNow;
        var res = await client.PostAsync($"/api/declarations/{declId}/kvgs-confirm", null);
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var row = await db.Declarations.AsNoTracking().SingleAsync(d => d.Id == declId);
        row.Status.Should().Be(DeclarationStatus.KVGSConfirmed);
        row.KvgsConfirmedByUserId.Should().NotBeNull();
        row.KvgsConfirmedAt.Should().NotBeNull();
        row.KvgsConfirmedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow.AddMinutes(1));

        (await AuditLogQuery.CountForEntityAsync(factory, nameof(Declaration), declId)).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("Feature", "F-09")]
    public async Task Should_operator_cannot_confirm_kvgs_returns_403()
    {
        await using var factory = new B2ApiFactory();
        var (declId, _) = await CreateAllExitedWithClientAsync(factory);
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);
        var res = await client.PostAsync($"/api/declarations/{declId}/kvgs-confirm", null);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Feature", "F-09")]
    public async Task Should_prevent_un_exit_after_kvgs_confirm()
    {
        await using var factory = new B2ApiFactory();
        var (declId, client) = await CreateAllExitedWithClientAsync(factory);

        Guid vehicleId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == declId)
                .Select(v => v.Id)
                .SingleAsync();
        }

        client.DefaultRequestHeaders.Remove("Authorization");
        var sup = await client.LoginAsNewSupervisorAsync();
        client.SetBearer(sup.AccessToken);
        (await client.PostAsync($"/api/declarations/{declId}/kvgs-confirm", null)).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Remove("Authorization");
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var unExit = await client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/un-exit",
            new UnExitVehicleRequest("Không được sửa sau KVGS"),
            TestJson.Options);
        unExit.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Feature", "F-09")]
    public async Task Should_not_confirm_kvgs_when_concurrent_un_exit()
    {
        await using var factory = new B2ApiFactory();
        var (declId, _) = await CreateAllExitedWithClientAsync(factory);

        Guid vehicleId;
        Guid adminUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == declId)
                .Select(v => v.Id)
                .SingleAsync();
            adminUserId = await db.Users.AsNoTracking()
                .Where(u => u.Role == B2.Domain.Users.Role.Admin)
                .Select(u => u.Id)
                .FirstAsync();
        }

        var confirmTask = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<B2.Application.Abstractions.IDeclarationRepository>();
            try
            {
                await repo.ConfirmKvgsAsync(
                    declId,
                    B2.Domain.Users.Role.Supervisor,
                    adminUserId,
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
            }
            catch (B2.Domain.DomainException)
            {
                // Expected when un-exit wins the Serializable race first.
            }
        });

        var unExitTask = Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<B2.Application.Abstractions.IDeclarationRepository>();
            try
            {
                await repo.UnExitVehicleAsync(vehicleId, CancellationToken.None);
            }
            catch (B2.Domain.DomainException)
            {
                // Expected when KVGS confirm wins the Serializable race first.
            }
        });

        await Task.WhenAll(confirmTask, unExitTask);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var entity = await db.Declarations.AsNoTracking().SingleAsync(d => d.Id == declId);
            var vehicle = await db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicleId);

            if (entity.Status == DeclarationStatus.KVGSConfirmed)
            {
                vehicle.ExitMarked.Should().BeTrue("KVGS confirm must not commit while a vehicle is un-exited");
            }
            else
            {
                entity.Status.Should().Be(DeclarationStatus.Open);
                vehicle.ExitMarked.Should().BeFalse();
            }
        }
    }

    [Fact]
    [Trait("Feature", "F-09")]
    public async Task Should_cannot_confirm_before_all_exited_returns_409()
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
                new VehicleAddRequest("30A88882", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var dto = await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        client.DefaultRequestHeaders.Remove("Authorization");
        var sup = await client.LoginAsNewSupervisorAsync();
        client.SetBearer(sup.AccessToken);
        var res = await client.PostAsync($"/api/declarations/{dto!.Id}/kvgs-confirm", null);
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var json = await res.Content.ReadAsStringAsync();
        json.Should().Contain("DECLARATION_NOT_READY_FOR_KVGS");
    }
}
