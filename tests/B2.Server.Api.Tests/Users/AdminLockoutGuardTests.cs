using System.Net;
using System.Net.Http.Json;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Users;
using FluentAssertions;

namespace B2.Server.Api.Tests.Users;

/// <summary>#158 — no update may leave the system without an active Admin.</summary>
public sealed class AdminLockoutGuardTests
{
    [Fact]
    [Trait("Issue", "158")]
    public async Task Admin_cannot_deactivate_own_account()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var res = await UpdateAsync(client, admin.UserId, new UserUpdateRequest(null, null, IsActive: false));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("CANNOT_DEACTIVATE_SELF");
    }

    [Fact]
    [Trait("Issue", "158")]
    public async Task Last_active_admin_cannot_be_demoted()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);

        var res = await UpdateAsync(client, admin.UserId, new UserUpdateRequest(null, UserRoleDto.Supervisor, null));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("LAST_ADMIN_LOCKOUT");
    }

    [Fact]
    [Trait("Issue", "158")]
    public async Task Another_admin_can_be_deactivated_while_one_remains()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        var create = await client.PostAsJsonAsync(
            "/api/users",
            new UserCreateRequest("admin_" + Guid.NewGuid().ToString("N")[..8], "Second Admin", UserRoleDto.Admin, "Temp-Admin-Pass-1!"),
            TestJson.Options);
        create.EnsureSuccessStatusCode();
        var second = (await create.Content.ReadFromJsonAsync<UserDto>(TestJson.Options))!;

        (await UpdateAsync(client, second.Id, new UserUpdateRequest(null, null, IsActive: false)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        // Now the seed admin is the last one again.
        (await UpdateAsync(client, admin.UserId, new UserUpdateRequest(null, UserRoleDto.Operator, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static Task<HttpResponseMessage> UpdateAsync(HttpClient client, Guid id, UserUpdateRequest request) =>
        client.PutAsJsonAsync($"/api/users/{id}", request, TestJson.Options);
}
