using B2.Desktop.Services.Users;
using B2.Desktop.ViewModels.Admin;
using B2.Shared.Contracts.Users;
using FluentAssertions;

namespace B2.Desktop.Tests.Users;

/// <summary>F-02 wireframe § 6.8 — "Lọc" + "Tìm theo tên" on the Users list.</summary>
public sealed class UserListFilterTests
{
    private static readonly UserDto[] Users =
    [
        User("admin", "Quản trị", active: true),
        User("nam", "Nguyễn Văn Nam", active: true),
        User("hung", "Phạm Văn Hùng", active: true),
        User("linh", "Trần Thị Linh", active: false),
    ];

    [Fact]
    [Trait("Requirement", "F-02")]
    public void Empty_search_and_All_returns_everyone()
    {
        UserListFilter.Apply(Users, "  ", UserStatusFilter.All).Should().HaveCount(4);
    }

    [Theory]
    [InlineData("hung", "hung")]
    [InlineData("HÙNG", "hung")]
    [InlineData("van nam", "nam")]
    [InlineData("lin", "linh")]
    public void Search_matches_name_or_username_ignoring_case_and_tone_marks(string term, string expected)
    {
        UserListFilter.Apply(Users, term, UserStatusFilter.All)
            .Select(u => u.Username).Should().Equal(expected);
    }

    [Fact]
    public void Status_filter_splits_active_and_inactive()
    {
        UserListFilter.Apply(Users, null, UserStatusFilter.Active)
            .Should().HaveCount(3).And.OnlyContain(u => u.IsActive);
        UserListFilter.Apply(Users, null, UserStatusFilter.Inactive)
            .Select(u => u.Username).Should().Equal("linh");
    }

    [Fact]
    public void Own_row_cannot_be_deactivated_but_others_can()
    {
        var self = new UserRow(Users[0], "Admin", IsSelf: true);
        var other = new UserRow(Users[1], "Operator", IsSelf: false);
        var inactive = new UserRow(Users[3], "Supervisor", IsSelf: false);

        self.CanDeactivate.Should().BeFalse();
        other.CanDeactivate.Should().BeTrue();
        other.CanReactivate.Should().BeFalse();
        inactive.CanDeactivate.Should().BeFalse();
        inactive.CanReactivate.Should().BeTrue();
    }

    private static UserDto User(string username, string fullName, bool active) =>
        new(Guid.NewGuid(), username, fullName, UserRoleDto.Operator, active, false, null, DateTimeOffset.UnixEpoch);
}
