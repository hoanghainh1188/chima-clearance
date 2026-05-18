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
    private static HttpRequestMessage ExitVehicleRequest(Guid vehicleId, string idempotencyKey)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, $"/api/vehicles/{vehicleId}/exit");
        msg.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return msg;
    }

    [Fact]
    [Trait("Feature", "F-14")]
    public async Task Should_by_officer_report_include_supervisor_with_only_kvgs_activity()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var opLogin = await client.LoginAsNewOperatorAsync();
        client.SetBearer(opLogin.AccessToken);
        var no = TestDeclarationNumbers.Next();
        var create = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                no,
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("31A20001", null, null)),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var dto = await create.Content.ReadFromJsonAsync<DeclarationDto>(TestJson.Options);
        var declId = dto!.Id;

        Guid vehicleId;
        DateOnly businessDate;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            vehicleId = await db.Vehicles.AsNoTracking()
                .Where(v => v.DeclarationId == declId)
                .Select(v => v.Id)
                .SingleAsync();
            businessDate = await db.Declarations.AsNoTracking()
                .Where(d => d.Id == declId)
                .Select(d => d.BusinessDate)
                .SingleAsync();
        }

        (await client.SendAsync(ExitVehicleRequest(vehicleId, Guid.NewGuid().ToString("N")))).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Remove("Authorization");
        var supLogin = await client.LoginAsNewSupervisorAsync();
        client.SetBearer(supLogin.AccessToken);
        (await client.PostAsync($"/api/declarations/{declId}/kvgs-confirm", content: null)).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Remove("Authorization");
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var d = businessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var report = await client.GetFromJsonAsync<List<OfficerReportItemDto>>(
            $"/api/reports/by-officer?from={d}&to={d}",
            TestJson.Options);

        var supervisorRow = report!.Single(x => x.OfficerUserId == supLogin.UserId);
        supervisorRow.VehiclesEntered.Should().Be(0);
        supervisorRow.DeclarationsKvgsConfirmed.Should().Be(1);

        var operatorRow = report.Single(x => x.OfficerUserId == opLogin.UserId);
        operatorRow.VehiclesEntered.Should().Be(1);
        operatorRow.DeclarationsKvgsConfirmed.Should().Be(0);
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
