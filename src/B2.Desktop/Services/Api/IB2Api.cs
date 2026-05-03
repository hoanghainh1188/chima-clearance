using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using Refit;

namespace B2.Desktop.Services.Api;

public interface IB2Api
{
    [Post("/api/auth/login")]
    Task<LoginResponse> LoginAsync([Body] LoginRequest request, CancellationToken cancellationToken = default);

    [Post("/api/auth/change-password")]
    Task ChangePasswordAsync([Body] ChangePasswordRequest request, CancellationToken cancellationToken = default);

    [Get("/api/users")]
    Task<UserListResponse> ListUsersAsync([Query] int page, [Query] int pageSize, CancellationToken cancellationToken = default);

    [Post("/api/users")]
    Task<UserDto> CreateUserAsync([Body] UserCreateRequest request, CancellationToken cancellationToken = default);
}
