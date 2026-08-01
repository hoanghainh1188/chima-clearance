namespace B2.Domain.Declarations;

/// <summary>
/// Optional trailer / container number (cột F): upper-cased and separator-stripped like the
/// plate, since both columns are pasted from the same internal system. Max 20 characters.
/// </summary>
public readonly record struct ContainerNumber
{
    public const int MaxLength = 20;

    public string? Value { get; }

    private ContainerNumber(string? value) => Value = value;

    public static ContainerNumber? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = PlateNumber.Normalize(raw);
        if (s.Length > MaxLength)
        {
            throw new DomainException("CONTAINER_NUMBER_TOO_LONG", $"Container number must be at most {MaxLength} characters.", 400);
        }

        return new ContainerNumber(s);
    }
}
