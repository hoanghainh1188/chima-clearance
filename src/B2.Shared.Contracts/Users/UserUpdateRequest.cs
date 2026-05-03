namespace B2.Shared.Contracts.Users;

public sealed record UserUpdateRequest(string? FullName, UserRoleDto? Role, bool? IsActive);
