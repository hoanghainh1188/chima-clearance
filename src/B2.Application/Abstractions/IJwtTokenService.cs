using B2.Domain.Users;

namespace B2.Application.Abstractions;

public interface IJwtTokenService
{
    string CreateAccessToken(Guid userId, Role role, bool mustChangePassword, DateTimeOffset utcNow, out DateTimeOffset expiresAt);

    string CreateRefreshToken();
}
