using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Users;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;

namespace B2.Application.Auth;

public sealed class LoginCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;

    public LoginCommandHandler(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IJwtTokenService jwt,
        IPasswordHasher hasher,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _jwt = jwt;
        _hasher = hasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = new Username(request.Username);
        var user = await _users.GetByUsernameAsync(username.Value, cancellationToken);
        if (user is null)
        {
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username or password.", 401);
        }

        user.EnsureNotLocked(_clock.UtcNow);

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new DomainException("INVALID_CREDENTIALS", "Invalid username or password.", 401);
        }

        user.RecordSuccessfulLogin(_clock.UtcNow);
        var access = _jwt.CreateAccessToken(user.Id, user.Role, user.MustChangePassword, _clock.UtcNow, out var exp);
        var refresh = _jwt.CreateRefreshToken();
        await _refreshTokens.StoreAsync(user.Id, refresh, _clock.UtcNow.AddDays(7), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var after = System.Text.Json.JsonSerializer.Serialize(new { user.LastLoginAt, user.FailedLoginCount });
        _audit.Set(new AuditEnvelope(user.Id, AuditAction.Update, nameof(User), user.Id, null, after, null));

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
