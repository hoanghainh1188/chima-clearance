namespace B2.Shared.Contracts.Users;

public sealed record UserCreateRequest(
    string Username,
    string FullName,
    UserRoleDto Role,
    string TemporaryPassword);
