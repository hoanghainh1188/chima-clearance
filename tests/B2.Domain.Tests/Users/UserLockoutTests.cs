using B2.Domain;
using B2.Domain.Users;
using FluentAssertions;

namespace B2.Domain.Tests.Users;

public sealed class UserLockoutTests
{
    [Fact]
    public void After_five_failed_logins_within_window_account_is_locked()
    {
        var t0 = new DateTimeOffset(2026, 5, 3, 8, 0, 0, TimeSpan.Zero);
        var user = new User(
            Guid.NewGuid(),
            new Username("test_op"),
            "$2a$10$placeholderhashplaceholderplaceholderplac",
            "Test User",
            Role.Operator,
            t0);

        for (var i = 0; i < 5; i++)
        {
            user.RecordFailedLogin(t0.AddMinutes(i));
        }

        user.IsLocked(t0.AddMinutes(1)).Should().BeTrue();
        user.Invoking(u => u.EnsureNotLocked(t0.AddMinutes(2)))
            .Should()
            .Throw<DomainException>()
            .Where(e => e.Code == "ACCOUNT_LOCKED");
    }

    [Fact]
    public void MustChangePassword_defaults_true_for_new_user()
    {
        var t0 = DateTimeOffset.UtcNow;
        var user = new User(
            Guid.NewGuid(),
            new Username("adminish"),
            "$2a$10$placeholderhashplaceholderplaceholderplac",
            "Admin",
            Role.Admin,
            t0);
        user.MustChangePassword.Should().BeTrue();
    }
}
