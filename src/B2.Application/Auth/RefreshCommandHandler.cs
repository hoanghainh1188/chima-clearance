using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Domain;
using B2.Domain.Users;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;

namespace B2.Application.Auth;

public sealed class RefreshCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IJwtTokenService _jwt;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IJwtTokenService jwt,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _jwt = jwt;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResponse> HandleAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var userId = await _refreshTokens.GetActiveUserIdAsync(request.RefreshToken, cancellationToken);
        if (userId is null)
        {
            throw new DomainException("REFRESH_INVALID", "Refresh token is invalid or expired.", 401);
        }

        var user = await _users.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new DomainException("REFRESH_INVALID", "Refresh token is invalid or expired.", 401);
        }

        user.EnsureNotLocked(_clock.UtcNow);
        await _refreshTokens.RevokeAsync(request.RefreshToken, cancellationToken);

        var access = _jwt.CreateAccessToken(user.Id, user.Role, user.MustChangePassword, _clock.UtcNow, out var exp);
        var refresh = _jwt.CreateRefreshToken();
        await _refreshTokens.StoreAsync(user.Id, refresh, _clock.UtcNow.AddDays(7), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            access,
            refresh,
            exp,
            user.MustChangePassword,
            RoleMapping.ToDto(user.Role),
            user.Id,
            user.FullName);
    }
}
