namespace B2.Domain.Declarations;

/// <summary>Optional trailer / container number (upper-cased, max 20).</summary>
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

        var s = raw.Trim().ToUpperInvariant();
        if (s.Length > MaxLength)
        {
            throw new DomainException("CONTAINER_NUMBER_TOO_LONG", $"Container number must be at most {MaxLength} characters.", 400);
        }

        return new ContainerNumber(s);
    }
}
