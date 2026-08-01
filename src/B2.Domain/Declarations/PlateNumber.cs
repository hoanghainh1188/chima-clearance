using System.Text.RegularExpressions;

namespace B2.Domain.Declarations;

public readonly record struct PlateNumber
{
    private static readonly Regex Pattern = new("^[0-9]{2}[A-Z]{1,2}[0-9]{4,5}$", RegexOptions.Compiled);

    /// <summary>
    /// Separators an operator may type or paste. Business rule cột E only ever shows the compact
    /// form (<c>12h05769</c> → <c>12H05769</c>), but the same column may be pasted from the unit's
    /// internal system, where plates carry a dash. Confirmed with the unit 2026-08-01: normalise
    /// rather than reject.
    /// </summary>
    private static readonly char[] Separators = [' ', ' ', '-', '–', '.', '_', '/'];

    public string Value { get; }

    private PlateNumber(string value) => Value = value;

    /// <summary>Upper-cases and strips separators. Does not validate.</summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim().ToUpperInvariant();
        if (trimmed.IndexOfAny(Separators) < 0)
        {
            return trimmed;
        }

        var buffer = new char[trimmed.Length];
        var written = 0;
        foreach (var ch in trimmed)
        {
            if (Array.IndexOf(Separators, ch) < 0)
            {
                buffer[written++] = ch;
            }
        }

        return new string(buffer, 0, written);
    }

    /// <summary>True when the value is a valid plate once normalised.</summary>
    public static bool IsValid(string? raw) => Pattern.IsMatch(Normalize(raw));

    public static PlateNumber Parse(string? raw)
    {
        var s = Normalize(raw);
        if (!Pattern.IsMatch(s))
        {
            throw new DomainException("PLATE_NUMBER_INVALID", "Plate number format is invalid.", 400);
        }

        return new PlateNumber(s);
    }
}
