namespace B2.Shared.Contracts.Admin;

public sealed record AuditLogListResponse(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<AuditLogItemDto> Items);
