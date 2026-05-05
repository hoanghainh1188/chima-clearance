namespace B2.Shared.Contracts.Reports;

public sealed record OfficerReportItemDto(
    Guid OfficerUserId,
    string OfficerName,
    int VehiclesEntered,
    int DeclarationsKvgsConfirmed);
