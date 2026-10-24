using System.Net.Http.Json;
using B2.Application.Common;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Declarations;

/// <summary>#164 — the KVGS queue (status=AllExited, no range) must include earlier business days.</summary>
public sealed class KvgsQueueListTests
{
    [Fact]
    [Trait("Issue", "164")]
    public async Task AllExited_without_a_range_includes_declarations_from_earlier_days()
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
        var decl = (await created.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;

        // Opened three days ago, last vehicle left today → AllExited, awaiting KVGS.
        var threeDaysAgo = VietnamTime.BusinessDateFromUtc(DateTimeOffset.UtcNow).AddDays(-3);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Declarations SET Status = 'AllExited', BusinessDate = {threeDaysAgo} WHERE Id = {decl.Id}");
        }

        var queue = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            "/api/declarations?status=AllExited", TestJson.Options);
        var todayOnly = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            "/api/declarations", TestJson.Options);

        queue!.Items.Should().ContainSingle(d => d.Id == decl.Id);
        todayOnly!.Items.Should().NotContain(d => d.Id == decl.Id, "the default ledger view stays scoped to today");
    }
}
