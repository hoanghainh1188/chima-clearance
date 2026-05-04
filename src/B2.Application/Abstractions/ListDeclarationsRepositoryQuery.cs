namespace B2.Application.Abstractions;

public sealed record ListDeclarationsRepositoryQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    string? StatusFilter,
    string? PlateFilter,
    IReadOnlyList<Guid>? ProcessorIds,
    IReadOnlyList<Guid>? YardIds,
    Guid? OfficerUserId,
    DateTimeOffset? SinceUtc,
    bool IncludeCancelled,
    int Page,
    int PageSize);
