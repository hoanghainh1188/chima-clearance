using System.Net;
using System.Net.Http.Json;
using B2.Application.Abstractions;
using B2.Application.Admin.Health;
using B2.Infrastructure.Diagnostics;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Admin;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

public sealed class HealthLogsEndpointTests
{
    [Fact]
    [Trait("Feature", "F-18")]
    public async Task Should_read_recent_logs_newest_first()
    {
        await using var factory = new B2ApiFactory();
        var reader = factory.Services.GetRequiredService<InMemoryLogReader>();
        var t0 = new DateTimeOffset(2026, 5, 4, 10, 0, 0, TimeSpan.Zero);
        reader.Append(t0, "Information", "oldest line");
        reader.Append(t0.AddMinutes(1), "Information", "middle line");
        reader.Append(t0.AddMinutes(2), "Warning", "newest line");

        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var response = await client.GetFromJsonAsync<LogListResponse>("/api/admin/logs", TestJson.Options);

        response.Should().NotBeNull();
        response!.Items.Should().HaveCountGreaterThanOrEqualTo(3);
        // Newest-first: our most recent injected line leads the others.
        var ours = response.Items.Where(i => i.Message.EndsWith("line", StringComparison.Ordinal)).ToList();
        ours[0].Message.Should().Be("newest line");
        ours.Select(i => i.Message).Should().ContainInOrder("newest line", "middle line", "oldest line");
    }

    [Fact]
    [Trait("Feature", "F-18")]
    public async Task Should_filter_logs_by_level()
    {
        await using var factory = new B2ApiFactory();
        var reader = factory.Services.GetRequiredService<InMemoryLogReader>();
        var t0 = new DateTimeOffset(2026, 5, 4, 11, 0, 0, TimeSpan.Zero);
        reader.Append(t0, "Information", "an info line");
        reader.Append(t0.AddMinutes(1), "Warning", "a warning line");

        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var response = await client.GetFromJsonAsync<LogListResponse>("/api/admin/logs?level=Warning", TestJson.Options);

        response.Should().NotBeNull();
        response!.Items.Should().OnlyContain(i => i.Level == "Warning");
        response.Items.Should().Contain(i => i.Message == "a warning line");
    }

    [Fact]
    [Trait("Feature", "F-18")]
    public async Task Should_forbid_operator_on_logs_and_health_detail()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);

        (await client.GetAsync("/api/admin/logs")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/admin/health/detail")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Feature", "F-18")]
    public async Task Should_return_health_detail_for_admin()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var report = await client.GetFromJsonAsync<HealthReportDto>("/api/admin/health/detail", TestJson.Options);

        report.Should().NotBeNull();
        report!.UptimeSeconds.Should().BeGreaterThanOrEqualTo(0);
        report.FreeDiskBytes.Should().BeGreaterThan(0);
        // The CI/dev disk has > 1 GB free, so no warning is expected here.
        report.DiskSpaceWarning.Should().BeFalse();
    }

    [Fact]
    [Trait("Issue", "177")]
    public async Task Database_size_includes_the_wal_and_shm_files()
    {
        await using var factory = new B2ApiFactory();
        _ = factory.CreateClient();
        var dir = Path.Combine(Path.GetTempPath(), $"b2-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var dbPath = Path.Combine(dir, "b2.db");
            await File.WriteAllBytesAsync(dbPath, new byte[1000]);
            await File.WriteAllBytesAsync(dbPath + "-wal", new byte[300]);
            await File.WriteAllBytesAsync(dbPath + "-shm", new byte[20]);
            await using var scope = factory.Services.CreateAsyncScope();
            var probe = new SystemHealthProbe(
                scope.ServiceProvider.GetRequiredService<B2DbContext>(),
                scope.ServiceProvider.GetRequiredService<IClock>(),
                dbPath);

            var snapshot = await probe.ReadAsync(CancellationToken.None);

            snapshot.DbFileBytes.Should().Be(1320);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [Trait("Feature", "F-18")]
    [InlineData(0L, true)]
    [InlineData(500L * 1024 * 1024, true)]
    [InlineData(2L * 1024 * 1024 * 1024, false)]
    public void Should_flag_low_disk_below_one_gigabyte(long freeBytes, bool expectedWarning)
    {
        HealthThresholds.IsLowDisk(freeBytes).Should().Be(expectedWarning);
    }
}
