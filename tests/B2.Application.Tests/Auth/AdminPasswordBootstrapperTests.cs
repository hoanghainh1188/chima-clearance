using B2.Application.Abstractions;
using B2.Application.Auth;
using B2.Domain.Users;
using FluentAssertions;
using NSubstitute;

namespace B2.Application.Tests.Auth;

/// <summary>F-01 / FR-017 — applying the installer's per-install initial Admin password.</summary>
public sealed class AdminPasswordBootstrapperTests
{
    private const string Pending = "Hk7m-Pq4R-xT2n-Wd9s";
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 8, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAdminBootstrapStore _store = Substitute.For<IAdminBootstrapStore>();

    public AdminPasswordBootstrapperTests()
    {
        _hasher.HashPassword(Arg.Any<string>()).Returns(ci => "hash:" + ci.Arg<string>());
    }

    private AdminPasswordBootstrapper Sut => new(_users, _hasher, _unitOfWork, _store);

    private User GivenAdmin(bool mustChangePassword)
    {
        var admin = new User(Guid.NewGuid(), new Username("admin"), "old-hash", "Admin", Role.Admin, T0, mustChangePassword);
        _users.GetByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns(admin);
        return admin;
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Pending_password_is_applied_to_an_admin_that_never_chose_one()
    {
        var admin = GivenAdmin(mustChangePassword: true);
        for (var i = 0; i < 5; i++)
        {
            admin.RecordFailedLogin(T0.AddMinutes(i));
        }

        _store.TryReadPendingPassword().Returns(Pending);

        var (outcome, adminId) = await Sut.RunAsync(CancellationToken.None);

        outcome.Should().Be(AdminBootstrapOutcome.Applied);
        adminId.Should().Be(admin.Id);
        admin.PasswordHash.Should().Be("hash:" + Pending);
        admin.MustChangePassword.Should().BeTrue();
        admin.IsLocked(T0.AddMinutes(6)).Should().BeFalse();
        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _store.DeletePending();
        });
        _store.Received(1).WriteState(AdminPasswordState.Initial);
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task A_password_the_admin_chose_is_never_overwritten()
    {
        var admin = GivenAdmin(mustChangePassword: false);
        _store.TryReadPendingPassword().Returns(Pending);

        var (outcome, _) = await Sut.RunAsync(CancellationToken.None);

        outcome.Should().Be(AdminBootstrapOutcome.AdminAlreadyRotated);
        admin.PasswordHash.Should().Be("old-hash");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _store.Received(1).DeletePending();
        _store.Received(1).WriteState(AdminPasswordState.Rotated);
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Without_a_pending_password_state_stays_initial_and_nothing_changes()
    {
        var admin = GivenAdmin(mustChangePassword: true);
        _store.TryReadPendingPassword().Returns((string?)null);

        var (outcome, _) = await Sut.RunAsync(CancellationToken.None);

        outcome.Should().Be(AdminBootstrapOutcome.NoPendingPassword);
        admin.PasswordHash.Should().Be("old-hash");
        _store.DidNotReceive().DeletePending();
        _store.Received(1).WriteState(AdminPasswordState.Initial);
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task A_weak_pending_password_is_discarded_not_applied()
    {
        var admin = GivenAdmin(mustChangePassword: true);
        _store.TryReadPendingPassword().Returns("short");

        var (outcome, _) = await Sut.RunAsync(CancellationToken.None);

        outcome.Should().Be(AdminBootstrapOutcome.RejectedWeakPassword);
        admin.PasswordHash.Should().Be("old-hash");
        _store.Received(1).DeletePending();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Missing_admin_discards_the_pending_password()
    {
        _users.GetByUsernameAsync("admin", Arg.Any<CancellationToken>()).Returns((User?)null);
        _store.TryReadPendingPassword().Returns(Pending);

        var (outcome, adminId) = await Sut.RunAsync(CancellationToken.None);

        outcome.Should().Be(AdminBootstrapOutcome.AdminMissing);
        adminId.Should().BeNull();
        _store.Received(1).DeletePending();
        _store.Received(1).WriteState(AdminPasswordState.Rotated);
    }
}
