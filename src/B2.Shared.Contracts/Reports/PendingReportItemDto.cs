namespace B2.Shared.Contracts.Reports;

public sealed record PendingReportItemDto(
    Guid DeclarationId,
    Guid VehicleId,
    string DeclarationNumber,
    string PlateNumber,
    DateTimeOffset EntryTime,
    double HoursInside);
