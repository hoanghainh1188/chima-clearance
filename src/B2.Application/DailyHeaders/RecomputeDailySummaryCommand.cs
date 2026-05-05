namespace B2.Application.DailyHeaders;

public sealed record RecomputeDailySummaryCommand(DateOnly Date, string? Reason, Guid? ActorUserId = null);
