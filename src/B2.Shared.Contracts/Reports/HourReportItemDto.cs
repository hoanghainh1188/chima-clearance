namespace B2.Shared.Contracts.Reports;

public sealed record HourReportItemDto(
    string BucketLabel,
    int ExitedCount);
