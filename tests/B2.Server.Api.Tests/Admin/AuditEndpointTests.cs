using System.Net;
using System.Net.Http.Json;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Admin;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

public sealed class AuditEndpointTests
{
    [Fact]
    [Trait("Feature", "F-16")]
    public async Task Should_filter_audit_by_entity()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                TestDeclarationNumbers.Next(),
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("31A50001", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var decl = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        Guid vehicleId;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking().Where(v => v.DeclarationId == decl.Id).Select(v => v.Id).SingleAsync();
        }

        var exit = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vehicleId}/exit");
        exit.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await client.SendAsync(exit)).EnsureSuccessStatusCode();

        var response = await client.GetFromJsonAsync<AuditLogListResponse>(
            $"/api/admin/audit?entityType=Vehicle&entityId={vehicleId}",
            TestJson.Options);

        response.Should().NotBeNull();
        response!.Items.Should().NotBeEmpty();
        response.Items.Should().OnlyContain(i => i.EntityType == "Vehicle" && i.EntityId == vehicleId);
        response.Items.Should().Contain(i => i.ActorUsername == "admin");
    }

    [Fact]
    [Trait("Feature", "F-16")]
    public async Task Should_have_no_mutating_audit_endpoint()
    {
        await using var factory = new B2ApiFactory();
        var dataSources = factory.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var auditEndpoints = dataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/admin/audit", StringComparison.Ordinal) == true)
            .ToList();

        auditEndpoints.Should().NotBeEmpty("the audit list endpoint exists");
        foreach (var ep in auditEndpoints)
        {
            var methods = ep.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new List<string>();
            methods.Should().OnlyContain(
                m => m == HttpMethods.Get,
                "the audit log is append-only — no POST/PUT/PATCH/DELETE surface (FR-003)");
        }
    }

    [Fact]
    [Trait("Feature", "F-16")]
    public async Task Should_forbid_operator_on_audit()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);

        var res = await client.GetAsync("/api/admin/audit");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
