using System.Net;
using System.Net.Http.Json;
using B2.Application.Abstractions;
using B2.Domain.Audit;
using B2.Infrastructure.Backup;
using B2.Infrastructure.DependencyInjection;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

public sealed class RestoreEndpointTests
{
    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_stage_then_apply_pending_restore()
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
            service.ApplyPending().Should().BeNull("nothing staged yet");

            await service.StageBytesAsync(restored, new RestoreStagingMetadata(Guid.NewGuid(), DateTimeOffset.UtcNow, "snapshot.bak"));
            File.Exists(dbPath + ".restore-pending").Should().BeTrue();

            service.ApplyPending().Should().NotBeNull();
            File.ReadAllBytes(dbPath).Should().Equal(restored);
            File.Exists(dbPath + ".restore-pending").Should().BeFalse("pending file consumed");
            File.Exists(dbPath + "-wal").Should().BeFalse("stale WAL sidecar removed");
            service.CommitPending();
            File.Exists(dbPath + ".pre-restore").Should().BeFalse("pre-restore removed after commit");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_support_commit_and_rollback_on_pending_restore()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"b2-restore-rb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "b2.db");
        try
        {
            var originalBytes = "ORIGINAL-DATABASE"u8.ToArray();
            var restoredBytes = "RESTORED-DATABASE"u8.ToArray();
            File.WriteAllBytes(dbPath, originalBytes);

            var service = new DatabaseRestoreService(dbPath);
            await service.StageBytesAsync(restoredBytes, new RestoreStagingMetadata(Guid.NewGuid(), DateTimeOffset.UtcNow, "snap.bak"));

            // Apply pending
            service.ApplyPending();
            File.ReadAllBytes(dbPath).Should().Equal(restoredBytes);
            File.Exists(dbPath + ".pre-restore").Should().BeTrue("backup kept before commit/rollback");

            // Rollback test
            service.RollbackPending();
            File.ReadAllBytes(dbPath).Should().Equal(originalBytes, "rolled back to original db");
            File.Exists(dbPath + ".pre-restore").Should().BeFalse("backup moved back to original");

            // Test commit
            await service.StageBytesAsync(restoredBytes, new RestoreStagingMetadata(Guid.NewGuid(), DateTimeOffset.UtcNow, "snap2.bak"));
            service.ApplyPending();
            File.Exists(dbPath + ".pre-restore").Should().BeTrue();
            service.CommitPending();
            File.Exists(dbPath + ".pre-restore").Should().BeFalse("commit removes pre-restore backup");
            File.ReadAllBytes(dbPath).Should().Equal(restoredBytes);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-026")]
    public async Task Restore_on_the_same_pc_needs_no_secret()
    {
        // Pre-UAT audit D2: restoring on the entry-gate PC that made the backup must not depend
        // on the recovery-code envelope; the server uses its own sealed install password.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var snapshot = await (await client.PostAsync("/api/admin/backup-now", content: null)).Content.ReadAsByteArrayAsync();

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(snapshot), "file", "snapshot.bak" },
            { new StringContent(string.Empty), "password" },
        };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Services.GetRequiredService<IDatabaseRestoreService>().ApplyPending().Should().NotBeNull();
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
        var installPassword = factory.Services.GetRequiredService<IBackupSecretsStore>().GetOrCreate().InstallPassword;

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(snapshot), "file", "snapshot.bak" },
            { new StringContent(installPassword), "password" },
        };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var restoreService = factory.Services.GetRequiredService<IDatabaseRestoreService>();
        var metadata = restoreService.ApplyPending();
        metadata.Should().NotBeNull();
        metadata!.SnapshotFileName.Should().Be("snapshot.bak");

        await RestoreAuditPersistence.PersistAfterApplyAsync(factory.Services, metadata);
        await factory.Services.EnsureDatabaseAsync();
        restoreService.CommitPending();

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

        using var form = new MultipartFormDataContent { { new StringContent("any-secret"), "password" } };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_reject_mutating_requests_while_restore_is_pending()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var snapshot = await (await client.PostAsync("/api/admin/backup-now", content: null)).Content.ReadAsByteArrayAsync();
        var installPassword = factory.Services.GetRequiredService<IBackupSecretsStore>().GetOrCreate().InstallPassword;

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(snapshot), "file", "snapshot.bak" },
            { new StringContent(installPassword), "password" },
        };
        (await client.PostAsync("/api/admin/restore", form)).StatusCode.Should().Be(HttpStatusCode.OK);

        var write = await client.PostAsJsonAsync(
            "/api/declarations",
            new DeclarationCreateRequest(
                [TestDeclarationNumbers.Next()],
                SeedIds.ProcessorId,
                "C1",
                "G1",
                SeedIds.YardId,
                new VehicleAddRequest("31A20002", null, null)),
            TestJson.Options);

        write.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
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
