namespace B2.Shared.Contracts.Declarations;

/// <param name="AsOfUtc">
/// Server clock when the query started. FR-028 polling clients use it as the next
/// <c>since</c> watermark: exits and KVGS confirmations also match <c>since</c>, so a
/// watermark built from <c>EnteredAt</c> alone re-detected the same change every tick.
/// </param>
public sealed record PagedDeclarationsResponse(
    IReadOnlyList<DeclarationSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    DateTimeOffset? AsOfUtc = null);
