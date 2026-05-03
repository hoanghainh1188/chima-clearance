using System.Text.RegularExpressions;

namespace B2.Domain.MasterData;

public sealed class Yard
{
    private static readonly Regex CodePattern = new("^[A-Z0-9]{1,20}$", RegexOptions.Compiled);

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618
    private Yard()
    {
    }
#pragma warning restore CS8618

    public Yard(Guid id, string code, string name, DateTimeOffset createdAt)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern.IsMatch(c))
        {
            throw new DomainException("YARD_CODE_INVALID", "Yard code must match ^[A-Z0-9]{1,20}$.", 400);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("YARD_NAME_REQUIRED", "Yard name is required.", 400);
        }

        Id = id;
        Code = c;
        Name = name.Trim();
        IsActive = true;
        CreatedAt = createdAt;
    }

    public void Update(string code, string name)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (!CodePattern.IsMatch(c))
        {
            throw new DomainException("YARD_CODE_INVALID", "Yard code must match ^[A-Z0-9]{1,20}$.", 400);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("YARD_NAME_REQUIRED", "Yard name is required.", 400);
        }

        Code = c;
        Name = name.Trim();
    }

    public void SetActive(bool active) => IsActive = active;
}
