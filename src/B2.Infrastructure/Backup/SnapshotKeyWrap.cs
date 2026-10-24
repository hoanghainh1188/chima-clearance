using System.Security.Cryptography;
using System.Text;

namespace B2.Infrastructure.Backup;

/// <summary>
/// FR-026 data-key wrapping shared by snapshot container v1 and v2: the 32-byte data key is
/// AES-256-GCM sealed under a KEK derived with PBKDF2-HMAC-SHA256 from the install password or
/// the recovery code.
/// </summary>
internal static class SnapshotKeyWrap
{
    internal const int KeyBytes = 32;
    internal const int SaltBytes = 16;
    internal const int NonceBytes = 12;
    internal const int TagBytes = 16;
    internal const int Pbkdf2Iterations = 100_000;

    /// <summary>Serialized size of one <see cref="Blob"/> in the v2 binary header.</summary>
    internal const int BlobBytes = SaltBytes + NonceBytes + KeyBytes + TagBytes;

    internal static Blob Wrap(byte[] dataKey, string secret, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var wrapped = new byte[dataKey.Length];
        var tag = new byte[TagBytes];
        var kek = DeriveKek(secret, salt, iterations);
        try
        {
            using var gcm = new AesGcm(kek, TagBytes);
            gcm.Encrypt(nonce, dataKey, wrapped, tag);
            return new Blob(salt, nonce, wrapped, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>Returns the data key, or null when <paramref name="secret"/> does not unwrap this blob.</summary>
    internal static byte[]? TryUnwrap(Blob blob, string secret, int iterations)
    {
        var kek = DeriveKek(secret, blob.Salt, iterations);
        var dataKey = new byte[blob.WrappedKey.Length];
        try
        {
            using var gcm = new AesGcm(kek, TagBytes);
            gcm.Decrypt(blob.Nonce, blob.WrappedKey, blob.Tag, dataKey);
            return dataKey;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(dataKey);
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static byte[] DeriveKek(string secret, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(secret), salt, iterations, HashAlgorithmName.SHA256, KeyBytes);

    internal sealed record Blob(byte[] Salt, byte[] Nonce, byte[] WrappedKey, byte[] Tag);
}
