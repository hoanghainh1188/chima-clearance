using System.Security.Cryptography;

namespace B2.Infrastructure.Backup;

/// <summary>
/// FR-026 — generates a printable recovery code / install secret. Uses a Crockford-style
/// base32 alphabet (no I/L/O/U) so the code is unambiguous when copied off a printout.
/// </summary>
public static class RecoveryCodeGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Generate(int length = 24)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        var bytes = RandomNumberGenerator.GetBytes(length);
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
