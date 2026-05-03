using B2.Application.Common;
using B2.Domain.Users;
using B2.Shared.Contracts.Users;

namespace B2.Application.Users;

public static class UserMappings
{
    public static UserDto ToDto(this User user) =>
        new(
            user.Id,
            user.Username,
            user.FullName,
            RoleMapping.ToDto(user.Role),
            user.IsActive,
            user.MustChangePassword,
            user.LastLoginAt,
            user.CreatedAt);
}
