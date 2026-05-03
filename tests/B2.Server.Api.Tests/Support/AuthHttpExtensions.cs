using System.Net.Http.Headers;
using System.Net.Http.Json;
using B2.Shared.Contracts.Auth;

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
            new LoginRequest("admin", SeedAdminPassword));
        first.EnsureSuccessStatusCode();
        var firstBody = (await first.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        if (!firstBody.MustChangePassword)
        {
            return firstBody;
        }

        client.SetBearer(firstBody.AccessToken);
        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest(SeedAdminPassword, BootstrappedAdminPassword));
        change.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var second = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin", BootstrappedAdminPassword));
        second.EnsureSuccessStatusCode();
        return (await second.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
    }
}
