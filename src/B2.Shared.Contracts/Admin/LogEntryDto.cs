namespace B2.Shared.Contracts.Admin;

public sealed record LogEntryDto(DateTimeOffset At, string Level, string Message);
