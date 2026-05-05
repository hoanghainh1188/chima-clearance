using B2.Application.Abstractions;
using B2.Shared.Contracts.Reports;

namespace B2.Application.Reports;

public sealed class ByProcessorReportQueryHandler
{
    private readonly IReportsRepository _reports;

    public ByProcessorReportQueryHandler(IReportsRepository reports) => _reports = reports;

    public Task<IReadOnlyList<ProcessorReportItemDto>> HandleAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken) =>
        _reports.GetByProcessorAsync(fromDate, toDate, includeCancelled, cancellationToken);
}

public sealed class ByYardReportQueryHandler
{
    private readonly IReportsRepository _reports;

    public ByYardReportQueryHandler(IReportsRepository reports) => _reports = reports;

    public Task<IReadOnlyList<YardReportItemDto>> HandleAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken) =>
        _reports.GetByYardAsync(fromDate, toDate, includeCancelled, cancellationToken);
}

public sealed class ByHourReportQueryHandler
{
    private readonly IReportsRepository _reports;

    public ByHourReportQueryHandler(IReportsRepository reports) => _reports = reports;

    public Task<IReadOnlyList<HourReportItemDto>> HandleAsync(
        DateOnly date,
        int bucketMinutes,
        bool includeCancelled,
        CancellationToken cancellationToken) =>
        _reports.GetByHourAsync(date, bucketMinutes, includeCancelled, cancellationToken);
}

public sealed class ByOfficerReportQueryHandler
{
    private readonly IReportsRepository _reports;

    public ByOfficerReportQueryHandler(IReportsRepository reports) => _reports = reports;

    public Task<IReadOnlyList<OfficerReportItemDto>> HandleAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken) =>
        _reports.GetByOfficerAsync(fromDate, toDate, includeCancelled, cancellationToken);
}

public sealed class PendingReportQueryHandler
{
    private readonly IReportsRepository _reports;

    public PendingReportQueryHandler(IReportsRepository reports) => _reports = reports;

    public Task<IReadOnlyList<PendingReportItemDto>> HandleAsync(
        DateTimeOffset asOfUtc,
        bool includeCancelled,
        CancellationToken cancellationToken) =>
        _reports.GetPendingAsync(asOfUtc, includeCancelled, cancellationToken);
}
