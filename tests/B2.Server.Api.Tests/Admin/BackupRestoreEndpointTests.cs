using System.Net;
using System.Net.Http.Json;
using System.Text;
using B2.Application.Abstractions;
using B2.Domain;
using B2.Domain.Declarations;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Declarations;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

public sealed class BackupRestoreEndpointTests
{
    private const string InstallPassword = "install-secret-Password-1!";
    private const string RecoveryCode = "B2-RECOVERY-CODE-TEST-9999";

    private static readonly byte[] SqliteMagic = "SQLite format 3\0"u8.ToArray();

    private static async Task<int> SeedDeclarationsAsync(HttpClient client, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var create = await client.PostAsJsonAsync(
                "/api/declarations",
                new DeclarationCreateRequest(
                    [TestDeclarationNumbers.Next()],
                    SeedIds.ProcessorId,
                    "Co",
                    "Hang",
                    SeedIds.YardId,
                    new VehicleAddRequest($"31A6{i:D4}", null, null)),
                TestJson.Options);
            create.EnsureSuccessStatusCode();
        }

        return count;
    }

    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-026")]
    public async Task Should_restore_round_trip_with_either_secret()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var seeded = await SeedDeclarationsAsync(client, 5);

        var service = factory.Services.GetRequiredService<IBackupSnapshotService>();
        var container = await SnapshotTestHelpers.CreateAsync(service, InstallPassword, RecoveryCode);

        var fromPassword = await SnapshotTestHelpers.DecryptAsync(service, container, InstallPassword);
        var fromRecovery = await SnapshotTestHelpers.DecryptAsync(service, container, RecoveryCode);

        // FR-026: either secret yields the identical decrypted database.
        fromPassword.Should().StartWith(SqliteMagic);
        fromRecovery.Should().Equal(fromPassword);

        // Restore round-trip: the decrypted DB holds the seeded declarations.
        string tableName;
        using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            tableName = db.Model.FindEntityType(typeof(Declaration))!.GetTableName()!;
        }

        (await CountRowsAsync(fromPassword, tableName)).Should().Be(seeded);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-026")]
    public async Task Should_reject_a_wrong_secret()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        await SeedDeclarationsAsync(client, 1);

        var service = factory.Services.GetRequiredService<IBackupSnapshotService>();
        var container = await SnapshotTestHelpers.CreateAsync(service, InstallPassword, RecoveryCode);

        var act = () => SnapshotTestHelpers.DecryptAsync(service, container, "not-the-right-secret");
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("BACKUP_INVALID_SECRET");
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_backup_now_returns_an_encrypted_file_and_audits()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        await SeedDeclarationsAsync(client, 2);

        var res = await client.PostAsync("/api/admin/backup-now", content: null);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/octet-stream");
        res.Content.Headers.ContentDisposition!.FileName!.Trim('"').Should().StartWith("b2-").And.EndWith(".bak");
        (await res.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(0);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        (await db.AuditLogs.AsNoTracking().CountAsync(a => a.EntityType == "BackupSnapshot")).Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Should_forbid_operator_from_backup()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();
        client.SetBearer(op.AccessToken);

        (await client.PostAsync("/api/admin/backup-now", content: null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<int> CountRowsAsync(byte[] dbBytes, string tableName)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"b2-restore-{Guid.NewGuid():N}.db");
        await File.WriteAllBytesAsync(tempPath, dbBytes);
        try
        {
            await using var connection = new SqliteConnection($"Data Source={tempPath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\"";
            var scalar = await command.ExecuteScalarAsync();
            return Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
