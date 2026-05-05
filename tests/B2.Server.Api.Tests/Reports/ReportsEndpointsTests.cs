using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.Reports;
using FluentAssertions;

namespace B2.Server.Api.Tests.Reports;

public sealed class ReportsEndpointsTests
{
    [Fact]
    [Trait("Feature", "F-14")]
    public async Task Should_hour_buckets_are_configurable()
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
                new VehicleAddRequest("31A10001", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();

        var date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var h30 = await client.GetFromJsonAsync<List<HourReportItemDto>>($"/api/reports/by-hour?date={date}&bucketMinutes=30", TestJson.Options);
        var h60 = await client.GetFromJsonAsync<List<HourReportItemDto>>($"/api/reports/by-hour?date={date}", TestJson.Options);
        h30.Should().NotBeNull();
        h60.Should().NotBeNull();
    }

    [Fact]
    [Trait("Feature", "F-14")]
    public async Task Should_pending_report_counts_vehicles_not_declarations()
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
                new VehicleAddRequest("31A10002", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var dto = await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        await client.PostAsJsonAsync(
            $"/api/declarations/{dto!.Id}/vehicles",
            new VehicleAddRequest("31A10003", null, null),
            TestJson.Options);
        var pending = await client.GetFromJsonAsync<List<PendingReportItemDto>>("/api/reports/pending", TestJson.Options);
        pending.Should().Contain(x => x.DeclarationId == dto.Id);
        pending!.Count(x => x.DeclarationId == dto.Id).Should().Be(2);
    }

    [Theory]
    [InlineData("/api/reports/by-processor?from=2026-01-01&to=2026-01-02")]
    [InlineData("/api/reports/by-yard?from=2026-01-01&to=2026-01-02")]
    [InlineData("/api/reports/by-hour?date=2026-01-01")]
    [InlineData("/api/reports/by-officer?from=2026-01-01&to=2026-01-02")]
    [InlineData("/api/reports/pending")]
    [Trait("Feature", "F-14")]
    public async Task Should_operator_forbidden_on_reports(string path)
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);

        var res = await client.GetAsync(path);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
