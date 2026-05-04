namespace B2.Shared.Contracts.Declarations;

public sealed record PagedDeclarationsResponse(
    IReadOnlyList<DeclarationSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
