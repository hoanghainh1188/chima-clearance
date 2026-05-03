namespace B2.Shared.Contracts.MasterData;

public sealed record YardListResponse(int Page, int PageSize, int Total, IReadOnlyList<YardDto> Items);
