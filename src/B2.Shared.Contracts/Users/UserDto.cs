namespace B2.Shared.Contracts.Users;

public sealed record UserDto(
    Guid Id,
    string Username,
    string FullName,
    UserRoleDto Role,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt);
