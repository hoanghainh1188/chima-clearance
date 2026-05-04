using System.ComponentModel;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;

namespace B2.Desktop.Services;

/// <summary>
/// Holds the post-login identity (FullName / Role / UserId) so the shell
/// chrome (wireframe § 5 user menu) and downstream services can read it
/// without each VM re-deriving from the JWT. Populated by
/// <c>LoginViewModel</c> on success and cleared on logout.
/// </summary>
public interface ISessionContext : INotifyPropertyChanged
{
    string FullName { get; }

    UserRoleDto Role { get; }

    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    void Set(LoginResponse response);

    void Clear();
}
