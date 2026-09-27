using B2.Application.Abstractions;
using B2.Application.Common;

namespace B2.Application.Auth;

/// <summary>
/// F-01 / FR-017 — applies the per-install initial Admin password that the installer sealed,
/// once, on service start (after migrations). A password the Admin chose themselves
/// (<c>MustChangePassword == false</c>) is never overwritten. The pending secret is deleted
/// only after the new hash is saved, so a crash in between re-applies it on the next start.
/// </summary>
public sealed class AdminPasswordBootstrapper
{
    /// <summary>Username of the Admin seeded by migration <c>0001_BaselineSchema</c>.</summary>
    public const string SeedAdminUsername = "admin";

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

        if (!admin.MustChangePassword)
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

    private void DiscardPending(string? pending)
    {
        if (pending is not null)
        {
            _store.DeletePending();
        }
    }
}
