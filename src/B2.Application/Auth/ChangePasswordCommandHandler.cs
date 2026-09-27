using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Domain;
using B2.Domain.Audit;
using B2.Domain.Users;
using B2.Shared.Contracts.Auth;

namespace B2.Application.Auth;

public sealed class ChangePasswordCommandHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditContext _audit;
    private readonly IAdminBootstrapStore _adminBootstrap;

    public ChangePasswordCommandHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        IClock clock,
        IUnitOfWork unitOfWork,
        IAuditContext audit,
        IAdminBootstrapStore adminBootstrap)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _adminBootstrap = adminBootstrap;
    }

    public async Task HandleAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!PasswordRules.MeetsComplexity(request.NewPassword))
        {
            throw new DomainException("PASSWORD_WEAK", "New password does not meet complexity rules.", 400);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new DomainException("USER_NOT_FOUND", "User not found.", 404);

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new DomainException("INVALID_CREDENTIALS", "Current password is incorrect.", 400);
        }

        var before = System.Text.Json.JsonSerializer.Serialize(new { mustChangePassword = user.MustChangePassword });
        user.SetPasswordHash(_hasher.HashPassword(request.NewPassword), mustChange: false);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (user.Username == AdminPasswordBootstrapper.SeedAdminUsername)
        {
            // F-01: from now on no installer run may replace this password (ADR-0009).
            _adminBootstrap.WriteState(AdminPasswordState.Rotated);
        }

        var after = System.Text.Json.JsonSerializer.Serialize(new { mustChangePassword = user.MustChangePassword });
        _audit.Set(new AuditEnvelope(userId, AuditAction.Update, nameof(User), user.Id, before, after, null));
    }
}
