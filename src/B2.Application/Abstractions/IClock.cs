namespace B2.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
