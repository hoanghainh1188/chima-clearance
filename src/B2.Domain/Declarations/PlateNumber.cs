using System.Text.RegularExpressions;

namespace B2.Domain.Declarations;

public readonly record struct PlateNumber
{
    private static readonly Regex Pattern = new("^[0-9]{2}[A-Z]{1,2}[0-9]{4,5}$", RegexOptions.Compiled);

    public string Value { get; }

    private PlateNumber(string value) => Value = value;

    public static PlateNumber Parse(string? raw)
    {
        var s = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (!Pattern.IsMatch(s))
        {
            throw new DomainException("PLATE_NUMBER_INVALID", "Plate number format is invalid.", 400);
        }

        return new PlateNumber(s);
    }
}
