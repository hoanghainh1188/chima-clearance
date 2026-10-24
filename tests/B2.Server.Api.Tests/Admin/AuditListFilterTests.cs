using System.Globalization;
using System.Net.Http.Json;
using B2.Domain.Audit;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Admin;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

public sealed class AuditListFilterTests
{
    private static readonly Guid EntityId = Guid.NewGuid();

    [Fact]
    [Trait("Issue", "155")]
    public async Task Date_filter_compares_instants_regardless_of_the_client_offset()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsAdminAsync()).AccessToken);
        // 2026-05-04 18:30Z = 2026-05-05 01:30 ICT — inside "05-05 (ICT)", outside "05-04 (ICT)".
        await SeedAsync(factory, new DateTimeOffset(2026, 5, 4, 18, 30, 0, TimeSpan.Zero));

        var may5 = await ListAsync(client, "2026-05-05T00:00:00+07:00", "2026-05-05T23:59:59+07:00");
        var may4 = await ListAsync(client, "2026-05-04T00:00:00+07:00", "2026-05-04T23:59:59+07:00");

        may5.Items.Should().ContainSingle();
        may4.Items.Should().BeEmpty();
    }

    [Fact]
    [Trait("Issue", "172")]
    public async Task Newest_entries_come_first()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        client.SetBearer((await client.LoginAsAdminAsync()).AccessToken);
        var older = new DateTimeOffset(2026, 5, 1, 1, 0, 0, TimeSpan.Zero);
        var newer = older.AddHours(5);
        await SeedAsync(factory, older, newer);

        var page = await client.GetFromJsonAsync<AuditLogListResponse>(
            $"/api/admin/audit?entityId={EntityId}&pageSize=1", TestJson.Options);

        page!.Total.Should().Be(2);
        page.Items.Should().ContainSingle().Which.At.Should().Be(newer);
    }

    private static Task<AuditLogListResponse> ListAsync(HttpClient client, string from, string to) =>
        client.GetFromJsonAsync<AuditLogListResponse>(
            $"/api/admin/audit?entityId={EntityId}&from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}",
            TestJson.Options)!;

    private static async Task SeedAsync(B2ApiFactory factory, params DateTimeOffset[] timestamps)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        foreach (var at in timestamps)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityType = "Probe",
                EntityId = EntityId,
                Action = AuditAction.Update,
                ChangesJson = "{}",
                ActorUserId = SeedIds.AdminUserId,
                At = at,
                CorrelationId = at.ToString("O", CultureInfo.InvariantCulture),
            });
        }

        await db.SaveChangesAsync();
    }
}
