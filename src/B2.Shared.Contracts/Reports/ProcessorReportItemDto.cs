namespace B2.Shared.Contracts.Reports;

public sealed record ProcessorReportItemDto(
    Guid ProcessorId,
    string ProcessorName,
    int TotalVehicles);
