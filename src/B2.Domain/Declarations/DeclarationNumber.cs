using System.Text.RegularExpressions;

namespace B2.Domain.Declarations;

public readonly record struct DeclarationNumber
{
    private static readonly Regex Digits12 = new("^[0-9]{12}$", RegexOptions.Compiled);

    public string Value { get; }

    private DeclarationNumber(string value) => Value = value;

    public static DeclarationNumber Parse(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        if (!Digits12.IsMatch(s))
        {
            throw new DomainException("DECLARATION_NUMBER_INVALID", "Declaration number must be exactly 12 digits.", 400);
        }

        return new DeclarationNumber(s);
    }
}
