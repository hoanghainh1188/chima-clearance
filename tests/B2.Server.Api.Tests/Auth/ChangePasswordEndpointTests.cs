using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using FluentAssertions;

namespace B2.Server.Api.Tests.Auth;

public sealed class ChangePasswordEndpointTests
{
    [Fact]
    [Trait("Feature", "F-01")]
    public async Task First_login_user_blocked_until_password_changed()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var create = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("first", "First Login", UserRoleDto.Operator, "First-Pass-1!"));
        create.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("first", "First-Pass-1!"));
        login.EnsureSuccessStatusCode();
        var body = (await login.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        body.MustChangePassword.Should().BeTrue();
        client.SetBearer(body.AccessToken);

        var blocked = await client.GetAsync("/api/users");
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await blocked.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(problem);
        doc.RootElement.GetProperty("code").GetString().Should().Be("PASSWORD_CHANGE_REQUIRED");
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Successful_change_password_returns_204_and_clears_must_change()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var create = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("rotate", "Rotate Me", UserRoleDto.Operator, "Old-Pass-12!"));
        create.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("rotate", "Old-Pass-12!"));
        var bodyOld = (await login.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        client.SetBearer(bodyOld.AccessToken);

        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("Old-Pass-12!", "Brand-New-456!"));
        change.StatusCode.Should().Be(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;
        var loginAgain = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("rotate", "Brand-New-456!"));
        loginAgain.EnsureSuccessStatusCode();
        var bodyNew = (await loginAgain.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        bodyNew.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Wrong_current_password_returns_400_invalid_credentials()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);

        var bad = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("not-the-current-pass", "Brand-New-456!"));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var doc = JsonDocument.Parse(await bad.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Weak_new_password_rejected_by_validator()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);

        var weak = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest(AuthHttpExtensions.BootstrappedAdminPassword, "short"));
        weak.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
