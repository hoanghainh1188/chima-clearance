using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace B2.Desktop.Services.Users;

/// <summary>
/// Client-side mirror of the server's account rules so the F-02 user dialog can reject bad
/// input before the round trip: <c>PasswordRules.MeetsComplexity</c> (Constitution VII,
/// ≥ 10 chars and 3 of 4 character classes) and the domain <c>Username</c> pattern. The server
/// stays authoritative. Pure BCL so the test project can link it.
/// </summary>
public static partial class TemporaryPassword
{
    public const int MinLength = 10;
    public const int GeneratedLength = 12;

    // Look-alike characters (0/O, 1/l/I) are left out: the Admin reads the password aloud
    // or copies it onto paper for the user.
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%*?";

    /// <summary>A random password that always satisfies <see cref="MeetsComplexity"/>.</summary>
    public static string Generate()
    {
        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[GeneratedLength];
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < chars.Length; i++)
        {
            chars[i] = Pick(all);
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    public static bool MeetsComplexity(string? password)
    {
        if (password is null || password.Length < MinLength)
        {
            return false;
        }

        var classes = new[]
        {
            password.Any(char.IsUpper),
            password.Any(char.IsLower),
            password.Any(char.IsDigit),
            password.Any(ch => !char.IsLetterOrDigit(ch)),
        };

        return classes.Count(x => x) >= 3;
    }

    /// <summary>Same rule as the domain <c>Username</c>: trimmed, lower-cased, ^[a-z][a-z0-9_]{2,49}$.</summary>
    public static bool IsValidUsername(string? username) =>
        !string.IsNullOrWhiteSpace(username)
        && UsernamePattern().IsMatch(username.Trim().ToLowerInvariant());

    [GeneratedRegex("^[a-z][a-z0-9_]{2,49}$")]
    private static partial Regex UsernamePattern();

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
