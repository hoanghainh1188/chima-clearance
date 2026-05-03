namespace B2.Shared.Contracts.Users;

public sealed record UserListResponse(int Page, int PageSize, int Total, IReadOnlyList<UserDto> Items);
