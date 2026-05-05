namespace B2.Shared.Contracts.Reports;

public sealed record YardReportItemDto(
    Guid YardId,
    string YardCode,
    string YardName,
    int TotalVehicles);
