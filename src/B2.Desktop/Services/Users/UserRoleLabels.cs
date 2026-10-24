using B2.Desktop.Resources.Strings;
using B2.Shared.Contracts.Users;

namespace B2.Desktop.Services.Users;

/// <summary>Display label for a role (wireframe § 6.8 "Vai trò" column and dialog picker).</summary>
public static class UserRoleLabels
{
    public static string For(UserRoleDto role) => role switch
    {
        UserRoleDto.Admin => AppResources.Users_Role_Admin,
        UserRoleDto.Supervisor => AppResources.Users_Role_Supervisor,
        _ => AppResources.Users_Role_Operator,
    };
}
