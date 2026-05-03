using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using FluentAssertions;

namespace B2.Server.Api.Tests.Auth;

public sealed class LoginEndpointTests
{
    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Successful_login_as_Operator_JWT_contains_role_Operator_and_8h_lifetime()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var create = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("nam", "Nam Operator", UserRoleDto.Operator, "Temp-Login-1!"));
        create.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nam", "Temp-Login-1!"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await login.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
        jwt.Claims.First(c => c.Type == "role").Value.Should().Be("Operator");
        (jwt.ValidTo - jwt.ValidFrom).Should().BeCloseTo(TimeSpan.FromHours(8), TimeSpan.FromMinutes(2));
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Locked_after_failures_sixth_attempt_returns_423()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var create = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("lockme", "Lock Me", UserRoleDto.Operator, "GoodPass-1234!"));
        create.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        for (var i = 0; i < 5; i++)
        {
            var fail = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("lockme", "wrong-password"));
            fail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var sixth = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("lockme", "GoodPass-1234!"));
        sixth.StatusCode.Should().Be((HttpStatusCode)423);
    }

    [Fact(Skip = "UI / desktop idle timer (wireframe F-01) — server has no idle session endpoint.")]
    [Trait("Feature", "F-01")]
    public Task Auto_logout_on_idle_is_desktop_concern() => Task.CompletedTask;
}
