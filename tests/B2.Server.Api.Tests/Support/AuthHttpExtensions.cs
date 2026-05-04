using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;

namespace B2.Server.Api.Tests.Support;

public static class AuthHttpExtensions
{
    /// <summary>Initial admin password seeded by migration <c>0001_BaselineSchema</c>.</summary>
    public const string SeedAdminPassword = "Change-Me-Now-1!";

    /// <summary>Stable admin password used by tests after bootstrap (FR-017 forced rotation).</summary>
    public const string BootstrappedAdminPassword = "Test-Admin-Pass-1!";

    public static void SetBearer(this HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    /// <summary>
    /// Logs in as the seeded admin. If <c>MustChangePassword</c> is still true (per FR-017,
    /// the admin always starts with the flag), rotates the password and re-logs in so tests
    /// receive an access token that bypasses <c>RequirePasswordChangeMiddleware</c>.
    /// </summary>
    public static async Task<LoginResponse> LoginAsAdminAsync(this HttpClient client)
    {
        var first = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin", SeedAdminPassword),
            TestJson.Options);
        if (first.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Same DB reused in one test: admin already rotated off the seed password (FR-017).
            first = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("admin", BootstrappedAdminPassword),
                TestJson.Options);
        }

        first.EnsureSuccessStatusCode();
        var firstBody = (await first.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        if (!firstBody.MustChangePassword)
        {
            return firstBody;
        }

        client.SetBearer(firstBody.AccessToken);
        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest(SeedAdminPassword, BootstrappedAdminPassword),
            TestJson.Options);
        change.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var second = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin", BootstrappedAdminPassword),
            TestJson.Options);
        second.EnsureSuccessStatusCode();
        return (await second.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
    }

    public const string BootstrappedOperatorPassword = "Test-Operator-Pass-1!";

    /// <summary>Creates a fresh Operator account (unique username) and returns a login that bypasses forced password change.</summary>
    public static async Task<LoginResponse> LoginAsNewOperatorAsync(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var username = "op_" + Guid.NewGuid().ToString("N")[..10];
        var create = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest(username, "Gate Operator", UserRoleDto.Operator, "Temp-Op-Pass-1!"),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var first = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, "Temp-Op-Pass-1!"),
            TestJson.Options);
        first.EnsureSuccessStatusCode();
        var firstBody = (await first.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        client.SetBearer(firstBody.AccessToken);
        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("Temp-Op-Pass-1!", BootstrappedOperatorPassword),
            TestJson.Options);
        change.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var second = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, BootstrappedOperatorPassword),
            TestJson.Options);
        second.EnsureSuccessStatusCode();
        return (await second.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
    }

    public static async Task<LoginResponse> LoginAsNewSupervisorAsync(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var adminLogin = await client.LoginAsAdminAsync();
        client.SetBearer(adminLogin.AccessToken);
        var username = "sup_" + Guid.NewGuid().ToString("N")[..10];
        var create = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest(username, "Supervisor User", UserRoleDto.Supervisor, "Temp-Sup-Pass-1!"),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var first = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, "Temp-Sup-Pass-1!"),
            TestJson.Options);
        first.EnsureSuccessStatusCode();
        var firstBody = (await first.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        client.SetBearer(firstBody.AccessToken);
        const string supPass = "Test-Supervisor-Pass-1!";
        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("Temp-Sup-Pass-1!", supPass),
            TestJson.Options);
        change.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var second = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, supPass),
            TestJson.Options);
        second.EnsureSuccessStatusCode();
        return (await second.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
    }
}
