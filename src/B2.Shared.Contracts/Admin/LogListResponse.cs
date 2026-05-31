namespace B2.Shared.Contracts.Admin;

public sealed record LogListResponse(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<LogEntryDto> Items);
