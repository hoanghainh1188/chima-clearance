namespace B2.Shared.Contracts.Declarations;

public sealed record DailyHeaderDto(
    DateOnly Date,
    string? OnDutyOfficerName,
    int? TotalVehicles,
    DateTimeOffset? LastSummaryComputedAt);
