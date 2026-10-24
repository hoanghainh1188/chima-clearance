using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2.Application.Abstractions;
using B2.Domain;
using B2.Infrastructure.Backup;
using B2.Server.Api.Tests.Support;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

/// <summary>
/// #150 / ADR-0011 — the streaming v2 snapshot container: multi-chunk round-trip, legacy v1
/// restore, and fail-closed behaviour on truncation, reordering and tampering.
/// </summary>
public sealed class SnapshotContainerTests : IDisposable
{
    private const string InstallPassword = "install-secret-Password-1!";
    private const string RecoveryCode = "B2-RECOVERY-CODE-TEST-9999";

    // Layout constants from ADR-0011 § 1 (two wraps, 1 MiB chunks).
    private const int HeaderBytes = 23 + (2 * 76);
    private const int ChunkSize = 1 << 20;
    private const int FullRecordBytes = 5 + ChunkSize + 16;

    // ~3.5 MiB of payload → at least four chunks, the last one short.
    private const int BlobBytes = (3 * ChunkSize) + (ChunkSize / 2);

    private static readonly byte[] SqliteMagic = "SQLite format 3\0"u8.ToArray();

    private readonly string _dir;
    private readonly string _dbPath;
    private readonly byte[] _blob;
    private readonly BackupSnapshotService _service;

    public SnapshotContainerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"b2-snapshot-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "b2.db");
        _blob = RandomNumberGenerator.GetBytes(BlobBytes);
        CreateDatabase(_dbPath, _blob);
        _service = new BackupSnapshotService(_dbPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-026")]
    public async Task V2_round_trips_a_multi_chunk_database_with_either_secret()
    {
        var container = await SnapshotTestHelpers.CreateAsync(_service, InstallPassword, RecoveryCode);

        container.AsSpan(0, 5).ToArray().Should().Equal("B2BK"u8.ToArray().Append((byte)2));
        container.Length.Should().BeGreaterThan(HeaderBytes + (3 * FullRecordBytes), "the DB spans several chunks");

        var fromPassword = await SnapshotTestHelpers.DecryptAsync(_service, container, InstallPassword);
        var fromRecovery = await SnapshotTestHelpers.DecryptAsync(_service, container, RecoveryCode);

        fromPassword.Should().StartWith(SqliteMagic);
        fromRecovery.Should().Equal(fromPassword);
        ReadBlob(fromPassword).Should().Equal(_blob);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-026")]
    public async Task V1_json_snapshots_still_restore_with_either_secret()
    {
        var database = await File.ReadAllBytesAsync(_dbPath);
        var legacy = BuildV1Container(database, InstallPassword, RecoveryCode);

        (await SnapshotTestHelpers.DecryptAsync(_service, legacy, InstallPassword)).Should().Equal(database);
        (await SnapshotTestHelpers.DecryptAsync(_service, legacy, RecoveryCode)).Should().Equal(database);

        var wrong = () => SnapshotTestHelpers.DecryptAsync(_service, legacy, "not-the-right-secret");
        (await wrong.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("BACKUP_INVALID_SECRET");
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task V2_rejects_a_wrong_secret()
    {
        var container = await SnapshotTestHelpers.CreateAsync(_service, InstallPassword, RecoveryCode);

        var act = () => SnapshotTestHelpers.DecryptAsync(_service, container, "not-the-right-secret");

        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("BACKUP_INVALID_SECRET");
    }

    [Theory]
    [Trait("Feature", "F-15")]
    [InlineData("mid-final-chunk")]
    [InlineData("whole-final-chunk")]
    [InlineData("at-first-record-boundary")]
    [InlineData("inside-header")]
    public async Task V2_fails_closed_on_truncation(string cut)
    {
        var container = await SnapshotTestHelpers.CreateAsync(_service, InstallPassword, RecoveryCode);
        var finalRecordStart = HeaderBytes + (ChunkCount(container) - 1) * FullRecordBytes;
        var length = cut switch
        {
            "mid-final-chunk" => container.Length - 100,
            "whole-final-chunk" => finalRecordStart,
            "at-first-record-boundary" => HeaderBytes + FullRecordBytes,
            _ => HeaderBytes - 10,
        };

        await ShouldBeCorruptAsync(container[..length]);
    }

    [Theory]
    [Trait("Feature", "F-15")]
    [InlineData("ciphertext")]
    [InlineData("tag")]
    [InlineData("nonce-prefix")]
    [InlineData("chunk-size")]
    [InlineData("final-flag")]
    public async Task V2_fails_closed_on_tampering(string target)
    {
        var container = await SnapshotTestHelpers.CreateAsync(_service, InstallPassword, RecoveryCode);
        var secondRecord = HeaderBytes + FullRecordBytes;
        var offset = target switch
        {
            "ciphertext" => secondRecord + 5 + 1234,
            "tag" => secondRecord + FullRecordBytes - 1,
            "nonce-prefix" => 14,
            "chunk-size" => 8,
            _ => HeaderBytes, // chunk 0's final flag: 0 → 1
        };
        container[offset] ^= 0x01;

        await ShouldBeCorruptAsync(container);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task V2_fails_closed_when_chunks_are_reordered()
    {
        var container = await SnapshotTestHelpers.CreateAsync(_service, InstallPassword, RecoveryCode);
        var first = container.AsSpan(HeaderBytes, FullRecordBytes).ToArray();
        var second = container.AsSpan(HeaderBytes + FullRecordBytes, FullRecordBytes).ToArray();
        second.CopyTo(container, HeaderBytes);
        first.CopyTo(container, HeaderBytes + FullRecordBytes);

        await ShouldBeCorruptAsync(container);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task V2_fails_closed_on_trailing_data()
    {
        var container = await SnapshotTestHelpers.CreateAsync(_service, InstallPassword, RecoveryCode);

        await ShouldBeCorruptAsync([.. container, 0x00]);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Rejects_a_file_that_is_not_a_snapshot()
    {
        await ShouldBeCorruptAsync("PK\u0003\u0004 not a snapshot"u8.ToArray());
        await ShouldBeCorruptAsync([]);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Restore_endpoint_stages_nothing_for_a_truncated_upload()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var snapshot = await (await client.PostAsync("/api/admin/backup-now", content: null)).Content.ReadAsByteArrayAsync();
        var installPassword = factory.Services.GetRequiredService<IBackupSecretsStore>().GetOrCreate().InstallPassword;

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(snapshot[..^20]), "file", "snapshot.bak" },
            { new StringContent(installPassword), "password" },
        };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("BACKUP_CORRUPT");
        factory.Services.GetRequiredService<IDatabaseRestoreService>().IsRestorePending().Should().BeFalse();
        factory.Restarter.Requests.Should().Be(0);
    }

    [Fact]
    [Trait("Feature", "F-15")]
    public async Task Restore_endpoint_accepts_a_legacy_v1_upload()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var legacy = BuildV1Container(await File.ReadAllBytesAsync(_dbPath), InstallPassword, RecoveryCode);

        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(legacy), "file", "legacy-v1.bak" },
            { new StringContent(RecoveryCode), "recoveryCode" },
        };
        var res = await client.PostAsync("/api/admin/restore", form);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Services.GetRequiredService<IDatabaseRestoreService>().ApplyPending()!.SnapshotFileName
            .Should().Be("legacy-v1.bak");
    }

    private static int ChunkCount(byte[] container) =>
        (int)Math.Ceiling((container.Length - HeaderBytes) / (double)FullRecordBytes);

    private async Task ShouldBeCorruptAsync(byte[] container)
    {
        var act = () => SnapshotTestHelpers.DecryptAsync(_service, container, InstallPassword);
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("BACKUP_CORRUPT");
    }

    private static void CreateDatabase(string path, byte[] blob)
    {
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE payload (data BLOB NOT NULL); INSERT INTO payload (data) VALUES ($data);";
        command.Parameters.AddWithValue("$data", blob);
        command.ExecuteNonQuery();
    }

    private byte[] ReadBlob(byte[] database)
    {
        var path = Path.Combine(_dir, $"restored-{Guid.NewGuid():N}.db");
        File.WriteAllBytes(path, database);
        using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT data FROM payload";
        return (byte[])command.ExecuteScalar()!;
    }

    /// <summary>Independent re-implementation of the pre-#150 v1 JSON writer.</summary>
    private static byte[] BuildV1Container(byte[] database, params string[] secrets)
    {
        var dataKey = RandomNumberGenerator.GetBytes(32);
        var wraps = secrets.Select(secret =>
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var kek = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, 100_000, HashAlgorithmName.SHA256, 32);
            var (nonce, ciphertext, tag) = Seal(kek, dataKey);
            return new { Salt = B64(salt), Nonce = nonce, WrappedKey = ciphertext, Tag = tag };
        }).ToArray();
        var (pNonce, pCiphertext, pTag) = Seal(dataKey, database);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = 1,
            Wraps = wraps,
            Payload = new { Nonce = pNonce, Ciphertext = pCiphertext, Tag = pTag },
        });
    }

    private static (string Nonce, string Ciphertext, string Tag) Seal(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag);
        return (B64(nonce), B64(ciphertext), B64(tag));
    }

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes);
}
