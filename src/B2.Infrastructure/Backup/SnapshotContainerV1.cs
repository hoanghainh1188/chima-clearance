using System.Security.Cryptography;
using System.Text.Json;
using B2.Domain;

namespace B2.Infrastructure.Backup;

/// <summary>
/// Read-only decoder for the legacy v1 JSON snapshot container (ADR-0011 § 5): one AES-256-GCM
/// payload and the two key wraps, every field Base64. New snapshots are always v2; this exists so
/// <c>.bak</c> files made before #150 still restore. v1 has no chunking, so the whole file is
/// decoded in memory — acceptable only for this bounded legacy set.
/// </summary>
internal static class SnapshotContainerV1
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    internal static async Task DecryptAsync(
        ReadOnlyMemory<byte> prefix,
        Stream rest,
        string secret,
        Stream destination,
        CancellationToken cancellationToken)
    {
        byte[] json;
        using (var buffer = new MemoryStream())
        {
            await buffer.WriteAsync(prefix, cancellationToken);
            await rest.CopyToAsync(buffer, cancellationToken);
            json = buffer.ToArray();
        }

        var model = Deserialize(json);
        foreach (var wrap in model.Wraps)
        {
            var dataKey = SnapshotKeyWrap.TryUnwrap(ToBlob(wrap), secret, SnapshotKeyWrap.Pbkdf2Iterations);
            if (dataKey is null)
            {
                continue;
            }

            byte[] plaintext;
            try
            {
                plaintext = DecryptPayload(dataKey, model.Payload);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }

            await destination.WriteAsync(plaintext, cancellationToken);
            return;
        }

        throw SnapshotErrors.InvalidSecret();
    }

    private static SnapshotContainer Deserialize(byte[] json)
    {
        try
        {
            var model = JsonSerializer.Deserialize<SnapshotContainer>(json, JsonOptions);
            if (model?.Wraps is null || model.Payload is null || model.Version != 1)
            {
                throw SnapshotErrors.Corrupt("Snapshot container could not be read.");
            }

            return model;
        }
        catch (JsonException)
        {
            throw SnapshotErrors.Corrupt("Snapshot container could not be read.");
        }
    }

    private static SnapshotKeyWrap.Blob ToBlob(WrapBlob wrap)
    {
        try
        {
            return new SnapshotKeyWrap.Blob(
                Convert.FromBase64String(wrap.Salt),
                Convert.FromBase64String(wrap.Nonce),
                Convert.FromBase64String(wrap.WrappedKey),
                Convert.FromBase64String(wrap.Tag));
        }
        catch (FormatException)
        {
            throw SnapshotErrors.Corrupt("Snapshot key wrap is malformed.");
        }
    }

    private static byte[] DecryptPayload(byte[] dataKey, PayloadBlob blob)
    {
        try
        {
            var nonce = Convert.FromBase64String(blob.Nonce);
            var ciphertext = Convert.FromBase64String(blob.Ciphertext);
            var tag = Convert.FromBase64String(blob.Tag);
            var plaintext = new byte[ciphertext.Length];
            using var gcm = new AesGcm(dataKey, SnapshotKeyWrap.TagBytes);
            gcm.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            throw SnapshotErrors.Corrupt("Snapshot payload failed authentication.");
        }
    }

    private sealed record WrapBlob(string Salt, string Nonce, string WrappedKey, string Tag);

    private sealed record PayloadBlob(string Nonce, string Ciphertext, string Tag);

    private sealed record SnapshotContainer(int Version, IReadOnlyList<WrapBlob> Wraps, PayloadBlob Payload);
}
