using System.Buffers.Binary;
using System.Security.Cryptography;

namespace B2.Infrastructure.Backup;

/// <summary>
/// ADR-0011 streaming snapshot container. A binary header (magic <c>B2BK</c>, version 2, chunk
/// size, PBKDF2 iterations, nonce prefix, FR-026 key wraps) is followed by independently
/// AES-256-GCM sealed chunks: <c>final(1) ‖ length(u32) ‖ ciphertext ‖ tag(16)</c>. Chunk
/// <i>i</i> uses nonce <c>prefix ‖ BE-u32(i)</c> and AAD <c>header ‖ BE-u32(i) ‖ final</c>, so
/// reordering, truncation and header tampering all fail authentication. Memory use is a few
/// chunk buffers regardless of the database size.
/// </summary>
internal static class SnapshotContainerV2
{
    internal const int MagicBytes = 4;
    internal const int DefaultChunkSize = 1 << 20;

    private const byte Version = 2;
    private const int MinChunkSize = 4 * 1024;
    private const int MaxChunkSize = 16 << 20;
    private const int MaxIterations = 10_000_000;
    private const int NoncePrefixBytes = 8;
    private const int MaxWraps = 8;
    private const int RecordHeaderBytes = 5;
    private const int AadSuffixBytes = 5;

    // magic(4) version(1) reserved(1) chunkSize(4) iterations(4) noncePrefix(8) wrapCount(1)
    private const int FixedHeaderBytes = MagicBytes + 1 + 1 + 4 + 4 + NoncePrefixBytes + 1;

    internal static ReadOnlySpan<byte> Magic => "B2BK"u8;

    /// <summary>Encrypts <paramref name="source"/> to <paramref name="destination"/> as a v2 container.</summary>
    internal static async Task WriteAsync(
        Stream source,
        Stream destination,
        string installPassword,
        string recoveryCode,
        CancellationToken cancellationToken)
    {
        var dataKey = RandomNumberGenerator.GetBytes(SnapshotKeyWrap.KeyBytes);
        var current = new byte[DefaultChunkSize];
        var next = new byte[DefaultChunkSize];
        var ciphertext = new byte[DefaultChunkSize];
        try
        {
            var noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixBytes);
            var header = BuildHeader(
                DefaultChunkSize,
                SnapshotKeyWrap.Pbkdf2Iterations,
                noncePrefix,
                [
                    SnapshotKeyWrap.Wrap(dataKey, installPassword, SnapshotKeyWrap.Pbkdf2Iterations),
                    SnapshotKeyWrap.Wrap(dataKey, recoveryCode, SnapshotKeyWrap.Pbkdf2Iterations),
                ]);
            await destination.WriteAsync(header, cancellationToken);

            using var gcm = new AesGcm(dataKey, SnapshotKeyWrap.TagBytes);
            var aad = NewAad(header);
            var record = new byte[RecordHeaderBytes];
            var tag = new byte[SnapshotKeyWrap.TagBytes];

            // Read one chunk ahead so the last chunk can be flagged final before it is sealed.
            var currentLength = await ReadFullAsync(source, current, cancellationToken);
            for (uint index = 0; ; index = checked(index + 1))
            {
                var nextLength = currentLength == current.Length
                    ? await ReadFullAsync(source, next, cancellationToken)
                    : 0;
                var final = nextLength == 0;

                SealChunk(gcm, noncePrefix, aad, index, final, current.AsSpan(0, currentLength), ciphertext, tag, record);
                await destination.WriteAsync(record, cancellationToken);
                await destination.WriteAsync(ciphertext.AsMemory(0, currentLength), cancellationToken);
                await destination.WriteAsync(tag, cancellationToken);

                if (final)
                {
                    break;
                }

                (current, next) = (next, current);
                currentLength = nextLength;
            }

            await destination.FlushAsync(cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(current);
            CryptographicOperations.ZeroMemory(next);
        }
    }

    /// <summary>
    /// Decrypts a v2 container whose <see cref="Magic"/> the caller has already consumed. On any
    /// failure <paramref name="destination"/> may hold partial plaintext and MUST be discarded.
    /// </summary>
    internal static async Task DecryptAsync(
        Stream container,
        string secret,
        Stream destination,
        CancellationToken cancellationToken)
    {
        var header = await ReadHeaderAsync(container, cancellationToken);
        var dataKey = UnwrapAny(header, secret) ?? throw SnapshotErrors.InvalidSecret();
        var plaintext = new byte[header.ChunkSize];
        var ciphertext = new byte[header.ChunkSize];
        try
        {
            using var gcm = new AesGcm(dataKey, SnapshotKeyWrap.TagBytes);
            var aad = NewAad(header.Bytes);
            var record = new byte[RecordHeaderBytes];
            var tag = new byte[SnapshotKeyWrap.TagBytes];

            for (uint index = 0; ; index = checked(index + 1))
            {
                await ReadExactAsync(container, record, cancellationToken);
                var (final, length) = ParseRecord(record, header.ChunkSize);
                await ReadExactAsync(container, ciphertext.AsMemory(0, length), cancellationToken);
                await ReadExactAsync(container, tag, cancellationToken);

                OpenChunk(gcm, header.NoncePrefix, aad, index, final, ciphertext.AsSpan(0, length), tag, plaintext.AsSpan(0, length));
                await destination.WriteAsync(plaintext.AsMemory(0, length), cancellationToken);

                if (final)
                {
                    break;
                }
            }

            if (await container.ReadAsync(new byte[1], cancellationToken) != 0)
            {
                throw SnapshotErrors.Corrupt("Snapshot has data after its final chunk.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dataKey);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] BuildHeader(int chunkSize, int iterations, byte[] noncePrefix, IReadOnlyList<SnapshotKeyWrap.Blob> wraps)
    {
        var header = new byte[FixedHeaderBytes + (wraps.Count * SnapshotKeyWrap.BlobBytes)];
        var span = header.AsSpan();
        Magic.CopyTo(span);
        span[4] = Version;
        span[5] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(span[6..], (uint)chunkSize);
        BinaryPrimitives.WriteUInt32LittleEndian(span[10..], (uint)iterations);
        noncePrefix.CopyTo(span[14..]);
        span[22] = (byte)wraps.Count;

        var offset = FixedHeaderBytes;
        foreach (var wrap in wraps)
        {
            foreach (var part in new[] { wrap.Salt, wrap.Nonce, wrap.WrappedKey, wrap.Tag })
            {
                part.CopyTo(span[offset..]);
                offset += part.Length;
            }
        }

        return header;
    }

    private static async Task<Header> ReadHeaderAsync(Stream container, CancellationToken cancellationToken)
    {
        var fixedPart = new byte[FixedHeaderBytes];
        Magic.CopyTo(fixedPart);
        await ReadExactAsync(container, fixedPart.AsMemory(MagicBytes), cancellationToken);

        if (fixedPart[4] != Version || fixedPart[5] != 0)
        {
            throw SnapshotErrors.Corrupt("Unsupported snapshot container version.");
        }

        var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(fixedPart.AsSpan(6));
        var iterations = BinaryPrimitives.ReadUInt32LittleEndian(fixedPart.AsSpan(10));
        var wrapCount = fixedPart[22];
        if (chunkSize is < MinChunkSize or > MaxChunkSize
            || iterations is < SnapshotKeyWrap.Pbkdf2Iterations or > MaxIterations
            || wrapCount is 0 or > MaxWraps)
        {
            throw SnapshotErrors.Corrupt("Snapshot header parameters are out of range.");
        }

        var bytes = new byte[FixedHeaderBytes + (wrapCount * SnapshotKeyWrap.BlobBytes)];
        fixedPart.CopyTo(bytes, 0);
        await ReadExactAsync(container, bytes.AsMemory(FixedHeaderBytes), cancellationToken);

        var wraps = new List<SnapshotKeyWrap.Blob>(wrapCount);
        for (var i = 0; i < wrapCount; i++)
        {
            var salt = FixedHeaderBytes + (i * SnapshotKeyWrap.BlobBytes);
            var nonce = salt + SnapshotKeyWrap.SaltBytes;
            var key = nonce + SnapshotKeyWrap.NonceBytes;
            var tag = key + SnapshotKeyWrap.KeyBytes;
            wraps.Add(new SnapshotKeyWrap.Blob(
                bytes[salt..nonce],
                bytes[nonce..key],
                bytes[key..tag],
                bytes[tag..(tag + SnapshotKeyWrap.TagBytes)]));
        }

        return new Header(bytes, (int)chunkSize, (int)iterations, fixedPart[14..22], wraps);
    }

    private static byte[]? UnwrapAny(Header header, string secret)
    {
        foreach (var wrap in header.Wraps)
        {
            var dataKey = SnapshotKeyWrap.TryUnwrap(wrap, secret, header.Iterations);
            if (dataKey is not null)
            {
                return dataKey;
            }
        }

        return null;
    }

    private static (bool Final, int Length) ParseRecord(byte[] record, int chunkSize)
    {
        var flag = record[0];
        var length = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(1));
        var final = flag == 1;
        if (flag > 1 || length > (uint)chunkSize || (!final && length != (uint)chunkSize))
        {
            throw SnapshotErrors.Corrupt("Snapshot chunk header is malformed.");
        }

        return (final, (int)length);
    }

    private static byte[] NewAad(byte[] header)
    {
        var aad = new byte[header.Length + AadSuffixBytes];
        header.CopyTo(aad, 0);
        return aad;
    }

    private static void SetChunkContext(Span<byte> nonce, byte[] noncePrefix, byte[] aad, uint index, bool final)
    {
        noncePrefix.CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce[NoncePrefixBytes..], index);
        var suffix = aad.AsSpan(aad.Length - AadSuffixBytes);
        BinaryPrimitives.WriteUInt32BigEndian(suffix, index);
        suffix[4] = final ? (byte)1 : (byte)0;
    }

    private static void SealChunk(
        AesGcm gcm,
        byte[] noncePrefix,
        byte[] aad,
        uint index,
        bool final,
        ReadOnlySpan<byte> plaintext,
        byte[] ciphertext,
        byte[] tag,
        byte[] record)
    {
        Span<byte> nonce = stackalloc byte[SnapshotKeyWrap.NonceBytes];
        SetChunkContext(nonce, noncePrefix, aad, index, final);
        gcm.Encrypt(nonce, plaintext, ciphertext.AsSpan(0, plaintext.Length), tag, aad);
        record[0] = final ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(1), (uint)plaintext.Length);
    }

    private static void OpenChunk(
        AesGcm gcm,
        byte[] noncePrefix,
        byte[] aad,
        uint index,
        bool final,
        ReadOnlySpan<byte> ciphertext,
        byte[] tag,
        Span<byte> plaintext)
    {
        Span<byte> nonce = stackalloc byte[SnapshotKeyWrap.NonceBytes];
        SetChunkContext(nonce, noncePrefix, aad, index, final);
        try
        {
            gcm.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        }
        catch (CryptographicException)
        {
            throw SnapshotErrors.Corrupt("Snapshot chunk failed authentication (tampered, reordered or wrong file).");
        }
    }

    private static async Task<int> ReadFullAsync(Stream source, byte[] buffer, CancellationToken cancellationToken) =>
        await source.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

    private static async Task ReadExactAsync(Stream source, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        if (await source.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken) < buffer.Length)
        {
            throw SnapshotErrors.Corrupt("Snapshot is truncated.");
        }
    }

    private sealed record Header(
        byte[] Bytes,
        int ChunkSize,
        int Iterations,
        byte[] NoncePrefix,
        IReadOnlyList<SnapshotKeyWrap.Blob> Wraps);
}
