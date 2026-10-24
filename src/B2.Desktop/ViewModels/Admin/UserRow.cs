using B2.Shared.Contracts.Users;

namespace B2.Desktop.ViewModels.Admin;

/// <summary>
/// One row of the F-02 Users table (wireframe § 6.8). <see cref="CanDeactivate"/> hides 🚫 on the
/// signed-in Admin's own row — the server refuses that with <c>CANNOT_DEACTIVATE_SELF</c> anyway.
/// </summary>
public sealed record UserRow(UserDto User, string RoleLabel, bool IsSelf)
{
    public string Username => User.Username;

    public string FullName => User.FullName;

    public bool IsActive => User.IsActive;

    public bool CanDeactivate => User.IsActive && !IsSelf;

    public bool CanReactivate => !User.IsActive;
}
