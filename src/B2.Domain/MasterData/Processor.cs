namespace B2.Domain.MasterData;

public sealed class Processor
{
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // EF Core materialization
    private Processor()
    {
    }
#pragma warning restore CS8618

    public Processor(Guid id, string fullName, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("PROCESSOR_NAME_REQUIRED", "Processor full name is required.", 400);
        }

        Id = id;
        FullName = fullName.Trim();
        IsActive = true;
        CreatedAt = createdAt;
    }

    public void Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainException("PROCESSOR_NAME_REQUIRED", "Processor full name is required.", 400);
        }

        FullName = fullName.Trim();
    }

    public void SetActive(bool active) => IsActive = active;
}
