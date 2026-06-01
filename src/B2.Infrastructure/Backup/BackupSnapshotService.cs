using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using B2.Application.Abstractions;
using B2.Domain;
using Microsoft.Data.Sqlite;

namespace B2.Infrastructure.Backup;

/// <summary>
/// F-15 / FR-026 encrypted snapshot codec. A random 32-byte data key (DK) encrypts the
/// SQLite bytes with AES-256-GCM; DK is wrapped twice (PBKDF2-SHA256 → AES-256-GCM) under
/// the install password and the recovery code, so either secret decrypts the snapshot.
/// </summary>
public sealed class BackupSnapshotService : IBackupSnapshotService
{
    private const int KeyBytes = 32;
    private const int SaltBytes = 16;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int Pbkdf2Iterations = 100_000;
    private const int ContainerVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _dbPath;

    public BackupSnapshotService(string dbPath) => _dbPath = dbPath;

    public async Task<byte[]> CreateSnapshotAsync(string installPassword, string recoveryCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(installPassword) || string.IsNullOrEmpty(recoveryCode))
        {
            throw new DomainException("BACKUP_SECRET_MISSING", "Install password and recovery code are both required.", 400);
        }

        var dbBytes = await SnapshotDatabaseAsync(cancellationToken);
        var dataKey = RandomNumberGenerator.GetBytes(KeyBytes);
        try
        {
            var payload = Encrypt(dataKey, dbBytes);
            var container = new SnapshotContainer(
                ContainerVersion,
                new[]
                {
                    WrapKey(dataKey, installPassword),
                    WrapKey(dataKey, recoveryCode),
                },
                payload);
            return JsonSerializer.SerializeToUtf8Bytes(container, JsonOptions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
        }
    }

    public byte[] DecryptSnapshot(byte[] container, string secret)
    {
        ArgumentNullException.ThrowIfNull(container);
        var model = JsonSerializer.Deserialize<SnapshotContainer>(container, JsonOptions)
            ?? throw new DomainException("BACKUP_CORRUPT", "Snapshot container could not be read.", 400);

        foreach (var wrap in model.Wraps)
        {
            var dataKey = TryUnwrap(wrap, secret);
            if (dataKey is null)
            {
                continue;
            }

            try
            {
                return Decrypt(dataKey, model.Payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }
        }

        throw new DomainException("BACKUP_INVALID_SECRET", "Neither the password nor the recovery code can decrypt this snapshot.", 400);
    }

    private async Task<byte[]> SnapshotDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_dbPath))
        {
            throw new DomainException("BACKUP_DB_MISSING", "The database file was not found.", 409);
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"b2-snapshot-{Guid.NewGuid():N}.db");
        try
        {
            // Pooling=False on both ends so the connections fully close on dispose and
            // release the temp-file handle before we read it (a pooled connection keeps the
            // OS file lock, which would fail the ReadAllBytes below on Windows).
            await using (var source = new SqliteConnection($"Data Source={_dbPath};Pooling=False"))
            {
                await source.OpenAsync(cancellationToken);
                await using var destination = new SqliteConnection($"Data Source={tempPath};Pooling=False");
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);
            }

            return await File.ReadAllBytesAsync(tempPath, cancellationToken);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static WrapBlob WrapKey(byte[] dataKey, string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var kek = DeriveKek(secret, salt);
        try
        {
            var sealed_ = Encrypt(kek, dataKey);
            return new WrapBlob(
                Convert.ToBase64String(salt),
                sealed_.Nonce,
                sealed_.Ciphertext,
                sealed_.Tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static byte[]? TryUnwrap(WrapBlob wrap, string secret)
    {
        var kek = DeriveKek(secret, Convert.FromBase64String(wrap.Salt));
        try
        {
            return Decrypt(kek, new PayloadBlob(wrap.Nonce, wrap.WrappedKey, wrap.Tag));
        }
        catch (CryptographicException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static byte[] DeriveKek(string secret, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeyBytes);

    private static PayloadBlob Encrypt(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        using var gcm = new AesGcm(key, TagBytes);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag);
        return new PayloadBlob(
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(ciphertext),
            Convert.ToBase64String(tag));
    }

    private static byte[] Decrypt(byte[] key, PayloadBlob blob)
    {
        var nonce = Convert.FromBase64String(blob.Nonce);
        var ciphertext = Convert.FromBase64String(blob.Ciphertext);
        var tag = Convert.FromBase64String(blob.Tag);
        var plaintext = new byte[ciphertext.Length];
        using var gcm = new AesGcm(key, TagBytes);
        gcm.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    private sealed record WrapBlob(string Salt, string Nonce, string WrappedKey, string Tag);

    private sealed record PayloadBlob(string Nonce, string Ciphertext, string Tag);

    private sealed record SnapshotContainer(int Version, IReadOnlyList<WrapBlob> Wraps, PayloadBlob Payload);
}
