using B2.Shared.Contracts.Reports;

namespace B2.Application.Abstractions;

public interface IReportsRepository
{
    Task<IReadOnlyList<ProcessorReportItemDto>> GetByProcessorAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YardReportItemDto>> GetByYardAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HourReportItemDto>> GetByHourAsync(
        DateOnly date,
        int bucketMinutes,
        bool includeCancelled,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficerReportItemDto>> GetByOfficerAsync(
        DateOnly fromDate,
        DateOnly toDate,
        bool includeCancelled,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingReportItemDto>> GetPendingAsync(
        DateTimeOffset asOfUtc,
        bool includeCancelled,
        CancellationToken cancellationToken);
}
