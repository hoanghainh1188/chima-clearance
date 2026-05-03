using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using FluentAssertions;

namespace B2.Server.Api.Tests.Users;

public sealed class UsersEndpointTests
{
    [Fact]
    [Trait("Feature", "F-02")]
    public async Task Create_user_linh_appears_in_list_as_Active()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var created = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("linh", "Linh Supervisor", UserRoleDto.Supervisor, "Tmp-1-Abcdef!"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetFromJsonAsync<UserListResponse>("/api/users", TestJson.Options);
        list!.Items.Should().Contain(u => u.Username == "linh" && u.IsActive);
    }

    [Fact]
    [Trait("Feature", "F-02")]
    public async Task Role_enforcement_Operator_POST_api_users_returns_403()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var createOp = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("op_gate", "Gate Operator", UserRoleDto.Operator, "Tmp-2-Abcdef!"));
        createOp.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var firstLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("op_gate", "Tmp-2-Abcdef!"));
        firstLogin.EnsureSuccessStatusCode();
        var firstBody = (await firstLogin.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        client.SetBearer(firstBody.AccessToken);

        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("Tmp-2-Abcdef!", "Solid-Pass-789!"));
        change.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = null;
        var freshLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("op_gate", "Solid-Pass-789!"));
        freshLogin.EnsureSuccessStatusCode();
        var freshBody = (await freshLogin.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        freshBody.MustChangePassword.Should().BeFalse();
        client.SetBearer(freshBody.AccessToken);

        var forbidden = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("blocked", "Blocked", UserRoleDto.Operator, "Tmp-3-Abcdef!"));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
