using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using B2.Application.Common;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Declarations;

public sealed class DailyHeadersEndpointTests
{
    // The validator enforces INV-11 against the Vietnam business date, not UTC.
    // Using DateTime.UtcNow.Date here would flake for ~7 hours/day on UTC runners
    // (CI Ubuntu / Windows agents) once UTC rolls past 17:00 = 00:00 ICT next day.
    private static DateOnly VietnamToday() => VietnamTime.BusinessDateFromUtc(DateTimeOffset.UtcNow);

    [Fact]
    [Trait("Feature", "F-11")]
    [Trait("Requirement", "FR-025")]
    public async Task Should_patch_officer_name_for_today_and_write_audit()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var today = VietnamToday();
        var todayStr = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var patchMessage = new HttpRequestMessage(HttpMethod.Patch, $"/api/daily-headers/{todayStr}/officer")
        {
            Content = JsonContent.Create(new UpdateOfficerNameRequest("Trực ban A"), options: TestJson.Options),
        };
        var patch = await client.SendAsync(patchMessage);
        patch.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var row = await db.DailyHeaders.SingleAsync(x => x.Date == today);
        row.OnDutyOfficerName.Should().Be("Trực ban A");
        (await db.AuditLogs.CountAsync(a => a.EntityType == "DailyHeader" && a.EntityId == row.Id)).Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Feature", "F-12")]
    public async Task Should_admin_recompute_daily_summary()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var date = VietnamToday().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var res = await client.PostAsJsonAsync(
            $"/api/daily-headers/{date}/recompute",
            new RecomputeDailySummaryRequest("manual"),
            TestJson.Options);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Feature", "F-11")]
    public async Task Should_list_daily_headers_in_range_after_patching_officer()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var today = VietnamToday();
        var todayStr = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var patchMessage = new HttpRequestMessage(HttpMethod.Patch, $"/api/daily-headers/{todayStr}/officer")
        {
            Content = JsonContent.Create(new UpdateOfficerNameRequest("Trực ban B"), options: TestJson.Options),
        };
        (await client.SendAsync(patchMessage)).EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<List<DailyHeaderDto>>(
            $"/api/daily-headers/?from={todayStr}&to={todayStr}",
            TestJson.Options);
        list.Should().ContainSingle(h => h.Date == today && h.OnDutyOfficerName == "Trực ban B");
    }

    [Theory]
    [Trait("Feature", "F-11")]
    [Trait("Requirement", "FR-025")]
    [InlineData(-1, HttpStatusCode.NoContent)]
    [InlineData(-2, HttpStatusCode.BadRequest)]
    [InlineData(1, HttpStatusCode.BadRequest)]
    public async Task Should_allow_officer_name_on_yesterdays_header_but_not_older_or_future(
        int dayOffset,
        HttpStatusCode expected)
    {
        // #186: the night shift ends after midnight, so yesterday's header must stay editable.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var dateStr = VietnamToday().AddDays(dayOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var patchMessage = new HttpRequestMessage(HttpMethod.Patch, $"/api/daily-headers/{dateStr}/officer")
        {
            Content = JsonContent.Create(new UpdateOfficerNameRequest("Trực ban đêm"), options: TestJson.Options),
        };
        var patch = await client.SendAsync(patchMessage);

        patch.StatusCode.Should().Be(expected);
    }
}
