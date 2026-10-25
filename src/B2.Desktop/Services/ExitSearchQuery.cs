namespace B2.Desktop.Services;

/// <summary>
/// Turns what the exit-gate officer typed into the 3–4 digit suffix the server accepts
/// (<c>VehicleSuffixSearchQueryValidator</c>: <c>^([0-9]{3,4})?$</c>). Officers often type five
/// digits or the whole plate (<c>29H-123.45</c>); rather than ignoring that silently (#192) the
/// last four digits are searched and <see cref="Adjusted"/> tells the view to say which digits it searched.
/// Pure BCL so the test project can link it.
/// </summary>
public sealed record ExitSearchQuery(string Suffix, bool Adjusted)
{
    private const int MinDigits = 3;
    private const int MaxDigits = 4;

    /// <summary>Empty suffix = list every vehicle still in the gate.</summary>
    public bool IsListAll => Suffix.Length == 0;

    /// <summary>
    /// Returns the query to send, or <c>null</c> while the input is still too short to search
    /// (one or two digits — wait for the officer to finish typing).
    /// </summary>
    public static ExitSearchQuery? Parse(string? raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new ExitSearchQuery(string.Empty, Adjusted: false);
        }

        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < MinDigits)
        {
            return null;
        }

        return digits.Length <= MaxDigits
            ? new ExitSearchQuery(digits, Adjusted: digits != trimmed)
            : new ExitSearchQuery(digits[^MaxDigits..], Adjusted: true);
    }
}
