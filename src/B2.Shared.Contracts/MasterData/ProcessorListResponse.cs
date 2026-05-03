namespace B2.Shared.Contracts.MasterData;

public sealed record ProcessorListResponse(int Page, int PageSize, int Total, IReadOnlyList<ProcessorDto> Items);
