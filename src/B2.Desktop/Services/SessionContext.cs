using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using CommunityToolkit.Mvvm.ComponentModel;

namespace B2.Desktop.Services;

/// <summary>
/// Default <see cref="ISessionContext"/> implementation. INPC properties so XAML
/// bindings (e.g. the user-menu button label) refresh on login / logout.
/// </summary>
public sealed partial class SessionContext : ObservableObject, ISessionContext
{
    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private UserRoleDto _role = UserRoleDto.Operator;

    [ObservableProperty]
    private Guid? _userId;

    [ObservableProperty]
    private bool _isAuthenticated;

    public void Set(LoginResponse response)
    {
        FullName = response.FullName;
        Role = response.Role;
        UserId = response.UserId;
        IsAuthenticated = true;
    }

    public void Clear()
    {
        FullName = string.Empty;
        Role = UserRoleDto.Operator;
        UserId = null;
        IsAuthenticated = false;
    }
}
