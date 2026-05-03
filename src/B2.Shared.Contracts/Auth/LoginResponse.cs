using B2.Shared.Contracts.Users;

namespace B2.Shared.Contracts.Auth;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    bool MustChangePassword,
    UserRoleDto Role,
    Guid UserId,
    string FullName);
