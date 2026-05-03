using B2.Domain.Users;
using B2.Shared.Contracts.Users;

namespace B2.Application.Common;

public static class RoleMapping
{
    public static UserRoleDto ToDto(Role role) => role switch
    {
        Role.Admin => UserRoleDto.Admin,
        Role.Operator => UserRoleDto.Operator,
        Role.Supervisor => UserRoleDto.Supervisor,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static Role ToDomain(UserRoleDto role) => role switch
    {
        UserRoleDto.Admin => Role.Admin,
        UserRoleDto.Operator => Role.Operator,
        UserRoleDto.Supervisor => Role.Supervisor,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };
}
