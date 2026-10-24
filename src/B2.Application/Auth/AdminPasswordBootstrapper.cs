using B2.Application.Abstractions;
using B2.Application.Common;

namespace B2.Application.Auth;

/// <summary>
/// F-01 / FR-017 — applies the per-install initial Admin password that the installer sealed,
/// once, on service start (after migrations). The pending secret is deleted only after the
/// new hash is saved, so a crash in between re-applies it on the next start.
///
/// "Still initial" must not be derived from <c>MustChangePassword</c> alone: an Admin reset by
/// another Admin also has it set, and replacing that password would break ADR-0009. So:
/// <list type="bullet">
///   <item><description>state <c>rotated</c> is one-way (also written by
///   <see cref="ChangePasswordCommandHandler"/> the moment <c>admin</c> changes password);</description></item>
///   <item><description>state <c>initial</c> holds while <c>MustChangePassword</c> is still set;</description></item>
///   <item><description>no state yet (install older than F-01): initial only if the hash is exactly
///   the unusable placeholder migration 0004 put on the untouched shared seed.</description></item>
/// </list>
/// </summary>
public sealed class AdminPasswordBootstrapper
{
    /// <summary>Username of the Admin seeded by migration <c>0001_BaselineSchema</c>.</summary>
    public const string SeedAdminUsername = "admin";

    /// <summary>
    /// Hash that migration <c>0004_RetireSharedAdminSeedHash</c> puts on the untouched shared seed:
    /// BCrypt of a random value discarded when the migration was written (matches no password).
    /// </summary>
    public const string RetiredSeedPlaceholderHash = "$2a$10$Rc6l/bZiNu2wd84mpsm8Q.90Xgwy433DRX6qrg62WdrnKgXqAcZxa";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminBootstrapStore _store;

    public AdminPasswordBootstrapper(
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork unitOfWork,
        IAdminBootstrapStore store)
    {
        _users = users;
        _hasher = hasher;
        _unitOfWork = unitOfWork;
        _store = store;
    }

    public async Task<(AdminBootstrapOutcome Outcome, Guid? AdminId)> RunAsync(CancellationToken cancellationToken)
    {
        var pending = _store.TryReadPendingPassword();
        var admin = await _users.GetByUsernameAsync(SeedAdminUsername, cancellationToken);

        if (admin is null)
        {
            DiscardPending(pending);
            _store.WriteState(AdminPasswordState.Rotated);
            return (AdminBootstrapOutcome.AdminMissing, null);
        }

        if (!IsStillInitial(admin.PasswordHash, admin.MustChangePassword, _store.ReadState()))
        {
            DiscardPending(pending);
            _store.WriteState(AdminPasswordState.Rotated);
            return (AdminBootstrapOutcome.AdminAlreadyRotated, admin.Id);
        }

        _store.WriteState(AdminPasswordState.Initial);
        if (pending is null)
        {
            return (AdminBootstrapOutcome.NoPendingPassword, admin.Id);
        }

        if (!PasswordRules.MeetsComplexity(pending))
        {
            _store.DeletePending();
            return (AdminBootstrapOutcome.RejectedWeakPassword, admin.Id);
        }

        admin.SetPasswordHash(_hasher.HashPassword(pending), mustChange: true);
        admin.ResetLockout();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _store.DeletePending();
        return (AdminBootstrapOutcome.Applied, admin.Id);
    }

    private static bool IsStillInitial(string passwordHash, bool mustChangePassword, AdminPasswordState? state) =>
        state switch
        {
            AdminPasswordState.Rotated => false,
            AdminPasswordState.Initial => mustChangePassword,
            _ => passwordHash == RetiredSeedPlaceholderHash,
        };

    private void DiscardPending(string? pending)
    {
        if (pending is not null)
        {
            _store.DeletePending();
        }
    }
}
