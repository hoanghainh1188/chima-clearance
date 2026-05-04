using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using B2.Application.Common;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Declarations;

public sealed class ListDeclarationsEndpointTests
{
    [Fact]
    [Trait("Feature", "F-10")]
    public async Task Should_default_view_shows_today_only()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var todayNo = TestDeclarationNumbers.Next();
        var createToday = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                todayNo,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("30B10001", null, null)),
            TestJson.Options);
        createToday.EnsureSuccessStatusCode();
        var todayDto = await createToday.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        var yesterdayNo = TestDeclarationNumbers.Next();
        var createY = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                yesterdayNo,
                SeedIds.ProcessorId,
                "C2",
                "G2",
                SeedIds.YardId,
                new VehicleAddRequest("30B10002", null, null)),
            TestJson.Options);
        createY.EnsureSuccessStatusCode();
        var yDto = await createY.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        var yesterday = VietnamTime.BusinessDateFromUtc(DateTimeOffset.UtcNow).AddDays(-1);
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            await db.Declarations.Where(d => d.Id == yDto!.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.BusinessDate, yesterday));
        }

        var list = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            "/api/declarations?page=1&pageSize=50",
            TestJson.Options);
        list!.Items.Should().Contain(x => x.Id == todayDto!.Id);
        list.Items.Should().NotContain(x => x.Id == yDto!.Id);
    }

    [Fact]
    [Trait("Feature", "F-10")]
    public async Task Should_order_by_entered_at_desc_when_same_day()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var firstNo = TestDeclarationNumbers.Next();
        var r1 = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                firstNo,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("30C20001", null, null)),
            TestJson.Options);
        r1.EnsureSuccessStatusCode();
        var dto1 = await r1.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        var secondNo = TestDeclarationNumbers.Next();
        var r2 = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                secondNo,
                SeedIds.ProcessorId,
                "C2",
                "G2",
                SeedIds.YardId,
                new VehicleAddRequest("30C20002", null, null)),
            TestJson.Options);
        r2.EnsureSuccessStatusCode();
        var dto2 = await r2.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        var list = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            "/api/declarations?page=1&pageSize=50",
            TestJson.Options);
        var idx1 = list!.Items.ToList().FindIndex(x => x.Id == dto1!.Id);
        var idx2 = list.Items.ToList().FindIndex(x => x.Id == dto2!.Id);
        idx1.Should().BeGreaterThanOrEqualTo(0);
        idx2.Should().BeGreaterThanOrEqualTo(0);
        idx2.Should().BeLessThan(idx1);
    }

    [Fact]
    [Trait("Feature", "F-10")]
    public async Task Should_live_updates_via_signalr_within_one_second()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var hubUrl = new Uri(factory.Server.BaseAddress, "hubs/events");
        var bag = new ConcurrentBag<Guid>();
        await using var conn = new HubConnectionBuilder()
            .WithUrl(
                hubUrl,
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(admin.AccessToken);
                })
            .WithAutomaticReconnect()
            .Build();
        conn.On<JsonElement>("LedgerChanged", payload =>
        {
            if (payload.TryGetProperty("declarationId", out var idProp) && idProp.TryGetGuid(out var id))
            {
                bag.Add(id);
            }
        });
        await conn.StartAsync();

        var no = TestDeclarationNumbers.Next();
        var created = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("30D30001", null, null)),
            TestJson.Options);
        created.EnsureSuccessStatusCode();
        var dto = await created.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        var ok = false;
        for (var i = 0; i < 50; i++)
        {
            if (bag.Any(g => g == dto!.Id))
            {
                ok = true;
                break;
            }

            await Task.Delay(20);
        }

        ok.Should().BeTrue("SignalR should push LedgerChanged within ~1 s");
        await conn.StopAsync();
    }

    [Fact]
    [Trait("Feature", "F-10")]
    public async Task Should_polling_since_returns_updated_declaration()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var since = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("O");
        var no = TestDeclarationNumbers.Next();
        var created = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("30E40001", null, null)),
            TestJson.Options);
        created.EnsureSuccessStatusCode();
        var dto = await created.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        var list = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            $"/api/declarations?since={Uri.EscapeDataString(since)}&page=1&pageSize=50",
            TestJson.Options);
        list!.Items.Should().Contain(x => x.Id == dto!.Id);
    }

    [Fact]
    [Trait("Feature", "F-10")]
    public async Task Should_accept_writes_during_polling_fallback_and_surface_in_since_delta()
    {
        // Spec.md F-10 Scenario 3 + FR-028: when SignalR has dropped and the
        // client is on the polling fallback (🟡), writes are still permitted
        // and the new row MUST surface in the next `since`-delta poll. The
        // server has no client-state gating, so this verifies both halves
        // (a) write accepted; (b) /api/declarations?since=… returns the row.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        // Capture the polling watermark just before the write — mirrors what
        // a desktop client in PollingFallback mode would send.
        var pollingSince = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O");

        var no = TestDeclarationNumbers.Next();
        var write = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "Polling Co",
                "Polling Goods",
                SeedIds.YardId,
                new VehicleAddRequest("30P40001", null, null)),
            TestJson.Options);

        write.EnsureSuccessStatusCode();
        var created = await write.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);

        // Polling delta — what the FR-028 yellow-state ConnectivityService
        // would request every ≤ 3 s — MUST surface the new row.
        var delta = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            $"/api/declarations?since={Uri.EscapeDataString(pollingSince)}&page=1&pageSize=50",
            TestJson.Options);

        delta!.Items.Should().Contain(x => x.Id == created!.Id,
            "FR-028: writes during 🟡 polling-fallback must converge to other clients via the `since` delta");
    }

    [Fact]
    [Trait("Feature", "F-10")]
    public async Task Should_pagination_returns_second_page()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        for (var i = 0; i < 12; i++)
        {
            var no = TestDeclarationNumbers.Next();
            var r = await client.PostAsJsonAsync(
                "/api/declarations",
                new DeclarationCreateRequest(
                    no,
                    SeedIds.ProcessorId,
                    "C1",
                    "G1",
                    SeedIds.YardId,
                    new VehicleAddRequest($"30F{10000 + i}", null, null)),
                TestJson.Options);
            r.EnsureSuccessStatusCode();
        }

        var page1 = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            "/api/declarations?page=1&pageSize=10",
            TestJson.Options);
        var page2 = await client.GetFromJsonAsync<PagedDeclarationsResponse>(
            "/api/declarations?page=2&pageSize=10",
            TestJson.Options);
        page1!.Items.Should().HaveCount(10);
        page2!.Items.Should().NotBeEmpty();
        page1.TotalCount.Should().BeGreaterThanOrEqualTo(12);
        page2.Items.Select(x => x.Id).Should().NotIntersectWith(page1.Items.Select(x => x.Id));
    }
}
