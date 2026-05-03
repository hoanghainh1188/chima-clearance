using B2.Application.Abstractions;

namespace B2.Infrastructure.Clock;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
