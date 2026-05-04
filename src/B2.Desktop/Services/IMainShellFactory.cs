using B2.Shared.Contracts.Users;

namespace B2.Desktop.Services;

/// <summary>
/// Builds the post-login shell (tabs by role). Kept as a factory because <see cref="UserRoleDto"/>
/// is only known after authentication.
/// </summary>
public interface IMainShellFactory
{
    Shell CreateMainShell(UserRoleDto role);
}
