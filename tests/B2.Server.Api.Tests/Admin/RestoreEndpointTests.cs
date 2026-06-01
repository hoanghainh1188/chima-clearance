using System.Net;
using B2.Domain.Audit;
using B2.Infrastructure.Backup;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

public sealed class RestoreEndpointTests
{
    // The dev-default install password the backup-now endpoint uses when B2:Backup:* is unset.
    private const string DevPassword = "b2-dev-install-password";

    [Fact]
    [Trait("Feature", "F-15")]
    public void Should_stage_then_apply_pending_restore()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-restore-svc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "b2.db");
        try
        {
            File.WriteAllBytes(dbPath, "OLD-DATABASE"u8.ToArray());
            File.WriteAllBytes(dbPath + "-wal", "stale-wal"u8.ToArray());
            var restored = "NEW-DATABASE-CONTENT"u8.ToArray();

            var service = new DatabaseRestoreService(dbPath);
            service.ApplyPending().Should().BeFalse("nothing staged yet");

            service.Stage(restored);
            File.Exists(dbPath + ".restore-pending").Should().BeTrue();

            service.ApplyPending().Should().BeTrue();
            File.ReadAllBytes(dbPath).Should().Equal(restored);
            File.Exists(dbPath + ".restore-pending").Should().BeFalse("pending file consumed");
            File.Exists(dbPath + "-wal").Should().BeFalse("stale WAL sidecar removed");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_accept_restore_upload_and_audit()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var snapshot = await (await client.PostAsync("/api/admin/backup-now", content: null)).Content.ReadAsByteArrayAsync();

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(snapshot), "file", "snapshot.bak" },
            { new StringContent(DevPassword), "password" },
        };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        (await db.AuditLogs.AsNoTracking().CountAsync(a => a.Action == AuditAction.Restore && a.EntityType == "DatabaseRestore"))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_reject_restore_with_wrong_secret()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var snapshot = await (await client.PostAsync("/api/admin/backup-now", content: null)).Content.ReadAsByteArrayAsync();

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(snapshot), "file", "snapshot.bak" },
            { new StringContent("totally-wrong-secret"), "password" },
        };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_require_a_file()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        using var form = new MultipartFormDataContent { { new StringContent(DevPassword), "password" } };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_forbid_operator_from_restore()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);

        using var form = new MultipartFormDataContent { { new StringContent("x"), "password" } };
        (await client.PostAsync("/api/admin/restore", form)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
