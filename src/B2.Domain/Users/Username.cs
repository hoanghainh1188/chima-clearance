using System.Text.RegularExpressions;

namespace B2.Domain.Users;

public readonly partial record struct Username
{
    private static readonly Regex Pattern = new("^[a-z][a-z0-9_]{2,49}$", RegexOptions.Compiled);

    public string Value { get; }

    public Username(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new DomainException("USERNAME_INVALID", "Username is required.", 400);
        }

        var v = raw.Trim().ToLowerInvariant();
        if (!Pattern.IsMatch(v))
        {
            throw new DomainException("USERNAME_INVALID", "Username format is invalid.", 400);
        }

        Value = v;
    }

    public static implicit operator string(Username u) => u.Value;
}
