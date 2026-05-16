using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using B2.Shared.Contracts.Reports;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Reports;

public sealed class ReportsEndpointsTests
{
    private static HttpRequestMessage ExitRequest(Guid vehicleId, string idemKey)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vehicleId}/exit");
        msg.Headers.TryAddWithoutValidation("Idempotency-Key", idemKey);
        return msg;
    }

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

    [Fact]
    [Trait("Feature", "F-14")]
    public async Task Should_by_officer_report_include_officer_who_only_confirmed_kvgs()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var no = TestDeclarationNumbers.Next();
        var plateSuffix = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var plate = $"30R{plateSuffix}";
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest(plate, null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var dto = (await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options))!;
        var declId = dto.Id;
        var businessDate = dto.BusinessDate;

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
        var operatorLogin = await client.LoginAsNewOperatorAsync();
        client.SetBearer(operatorLogin.AccessToken);
        (await client.SendAsync(ExitRequest(vehicleId, Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Remove("Authorization");
        var sup = await client.LoginAsNewSupervisorAsync();
        client.SetBearer(sup.AccessToken);
        (await client.PostAsync($"/api/declarations/{declId}/kvgs-confirm", null)).EnsureSuccessStatusCode();

        var reportClient = factory.CreateClient();
        var adminForReport = await reportClient.LoginAsAdminAsync();
        reportClient.SetBearer(adminForReport.AccessToken);
        var from = businessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var officers = await reportClient.GetFromJsonAsync<List<OfficerReportItemDto>>(
            $"/api/reports/by-officer?from={from}&to={from}",
            TestJson.Options);

        var kvgsOnly = officers!.Single(o => o.OfficerUserId == sup.UserId);
        kvgsOnly.VehiclesEntered.Should().Be(0);
        kvgsOnly.DeclarationsKvgsConfirmed.Should().Be(1);
        kvgsOnly.OfficerName.Should().Be(sup.FullName);
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
