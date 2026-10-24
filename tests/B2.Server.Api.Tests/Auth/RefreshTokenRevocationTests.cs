using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using FluentAssertions;

namespace B2.Server.Api.Tests.Auth;

/// <summary>#159 — credential / status changes must end every existing session of the account.</summary>
public sealed class RefreshTokenRevocationTests
{
    [Fact]
    [Trait("Issue", "159")]
    public async Task Admin_password_reset_revokes_existing_refresh_tokens()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();

        await AsAdminAsync(client, c => c.PostAsJsonAsync(
            $"/api/users/{op.UserId}/reset-password",
            new PasswordResetRequest("Reset-Op-Pass-1!"),
            TestJson.Options));

        (await RefreshAsync(client, op.RefreshToken)).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    [Trait("Issue", "159")]
    public async Task Deactivation_revokes_existing_refresh_tokens()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();

        await AsAdminAsync(client, c => c.PutAsJsonAsync(
            $"/api/users/{op.UserId}",
            new UserUpdateRequest(null, null, IsActive: false),
            TestJson.Options));
        await AsAdminAsync(client, c => c.PutAsJsonAsync(
            $"/api/users/{op.UserId}",
            new UserUpdateRequest(null, null, IsActive: true),
            TestJson.Options));

        // Re-activation must not revive the pre-deactivation session.
        (await RefreshAsync(client, op.RefreshToken)).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    [Trait("Issue", "159")]
    public async Task Own_password_change_revokes_existing_refresh_tokens()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();

        client.SetBearer(op.AccessToken);
        var change = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest(AuthHttpExtensions.BootstrappedOperatorPassword, "Changed-Op-Pass-2!"),
            TestJson.Options);
        change.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        (await RefreshAsync(client, op.RefreshToken)).IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    [Trait("Issue", "159")]
    public async Task Profile_edit_keeps_refresh_tokens_valid()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();

        await AsAdminAsync(client, c => c.PutAsJsonAsync(
            $"/api/users/{op.UserId}",
            new UserUpdateRequest("Renamed Operator", null, null),
            TestJson.Options));

        var ok = await RefreshAsync(client, op.RefreshToken);
        ok.IsSuccessStatusCode.Should().BeTrue(await ok.Content.ReadAsStringAsync());
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Refresh_rotates_the_token_and_rejects_reuse()
    {
        // Regression: GetActiveUserIdAsync compared DateTimeOffset in SQL, which the SQLite
        // provider cannot translate — every /refresh returned 500.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();

        var first = await RefreshAsync(client, op.RefreshToken);
        first.IsSuccessStatusCode.Should().BeTrue(await first.Content.ReadAsStringAsync());
        var rotated = (await first.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        rotated.RefreshToken.Should().NotBe(op.RefreshToken);

        (await RefreshAsync(client, op.RefreshToken)).IsSuccessStatusCode.Should().BeFalse();
        (await RefreshAsync(client, rotated.RefreshToken)).IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    [Trait("Issue", "149")]
    public async Task Concurrent_refreshes_with_one_token_mint_exactly_one_new_pair()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var op = await client.LoginAsNewOperatorAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RefreshAsync(client, op.RefreshToken)));

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1, "a refresh token is single-use");
        responses.Where(r => !r.IsSuccessStatusCode)
            .Should().OnlyContain(r => r.StatusCode == System.Net.HttpStatusCode.Unauthorized);
    }

    private static async Task AsAdminAsync(HttpClient client, Func<HttpClient, Task<HttpResponseMessage>> call)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        (await call(client)).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(refreshToken), TestJson.Options);
}
