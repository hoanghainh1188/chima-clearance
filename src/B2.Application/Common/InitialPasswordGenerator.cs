using System.Security.Cryptography;

namespace B2.Application.Common;

/// <summary>
/// F-01 — generates the per-install initial Admin password shown by the installer.
/// Four groups of four characters joined by '-' (e.g. <c>Hk7m-Pq4R-xT2n-Wd9s</c>): easy to
/// read aloud and copy by hand, ~91 bits of entropy, and always passes
/// <see cref="PasswordRules.MeetsComplexity"/> (upper, lower, digit and the '-' separator).
/// Pure BCL so the installer can compile this file directly (linked, not referenced).
/// </summary>
public static class InitialPasswordGenerator
{
    // No I, l, O, o, 0, 1 — the characters operators confuse when copying from a screen.
    private const string Upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghjkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Alphabet = Upper + Lower + Digits;

    private const int GroupCount = 4;
    private const int GroupLength = 4;
    private const char Separator = '-';

    public static string Generate()
    {
        while (true)
        {
            var chars = new char[GroupCount * GroupLength];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }

            // Re-draw (rare) until every class is present, rather than forcing positions.
            if (chars.Any(c => Upper.Contains(c)) && chars.Any(c => Lower.Contains(c)) && chars.Any(c => Digits.Contains(c)))
            {
                return string.Join(Separator, Enumerable.Range(0, GroupCount)
                    .Select(g => new string(chars, g * GroupLength, GroupLength)));
            }
        }
    }
}
