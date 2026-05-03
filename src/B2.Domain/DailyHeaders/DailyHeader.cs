namespace B2.Domain.DailyHeaders;

public sealed class DailyHeader
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string? OnDutyOfficerName { get; set; }
    public int? TotalVehicles { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSummaryComputedAt { get; set; }
}
