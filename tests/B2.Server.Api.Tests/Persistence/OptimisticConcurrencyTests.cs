using System.Net;
using System.Net.Http.Json;
using B2.Domain.Declarations;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Persistence;

/// <summary>#178 — RowVersion must change on every update, or the concurrency check is a no-op.</summary>
public sealed class OptimisticConcurrencyTests
{
    [Fact]
    [Trait("Issue", "178")]
    public async Task Stale_vehicle_update_throws_concurrency_exception()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsAdminAsync()).AccessToken);
        var created = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()], SeedIds.ProcessorId, "C1", "G1", SeedIds.YardId,
                new VehicleAddRequest("29A12345", null, null)),
            TestJson.Options);
        created.EnsureSuccessStatusCode();

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<B2DbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<B2DbContext>();
        var a = await dbA.Vehicles.Include(v => v.Declaration).SingleAsync();
        var b = await dbB.Vehicles.Include(v => v.Declaration).SingleAsync();
        var originalToken = a.RowVersion.ToArray();

        a.UpdateNote("ghi chú A");
        await dbA.SaveChangesAsync();
        a.RowVersion.Should().NotEqual(originalToken, "every save must issue a fresh token");

        b.UpdateNote("ghi chú B");
        var stale = () => dbB.SaveChangesAsync();

        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    [Trait("Issue", "178")]
    public async Task Stale_declaration_update_throws_concurrency_exception()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsAdminAsync()).AccessToken);
        (await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()], SeedIds.ProcessorId, "C1", "G1", SeedIds.YardId,
                new VehicleAddRequest("29A12345", null, null)),
            TestJson.Options)).EnsureSuccessStatusCode();

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<B2DbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<B2DbContext>();
        var a = await dbA.Declarations.Include(d => d.Vehicles).SingleAsync();
        var b = await dbB.Declarations.Include(d => d.Vehicles).SingleAsync();

        a.Cancel(Domain.Users.Role.Admin, SeedIds.AdminUserId, "lý do A", DateTimeOffset.UtcNow);
        await dbA.SaveChangesAsync();

        b.Cancel(Domain.Users.Role.Admin, SeedIds.AdminUserId, "lý do B", DateTimeOffset.UtcNow);
        var stale = () => dbB.SaveChangesAsync();

        await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
        a.Status.Should().Be(DeclarationStatus.Cancelled);
    }
}
