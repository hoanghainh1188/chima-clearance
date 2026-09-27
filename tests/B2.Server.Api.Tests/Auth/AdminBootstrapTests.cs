using System.Net;
using System.Net.Http.Json;
using B2.Application.Auth;
using B2.Infrastructure.Auth;
using B2.Infrastructure.Persistence;
using B2.Infrastructure.Migrations;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Auth;
using B2.Shared.Contracts.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Auth;

/// <summary>
/// F-01 / FR-017 — the shared seed password is retired (migration 0004) and the installer's
/// per-install initial password is applied on start, through the real store and startup path.
/// </summary>
public sealed class AdminBootstrapTests
{
    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Installer_password_logs_in_with_forced_change_and_shared_seed_password_does_not()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();

        var shared = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin", AuthHttpExtensions.RetiredSharedSeedPassword));
        var initial = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin", AuthHttpExtensions.InitialAdminPassword));

        shared.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        initial.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await initial.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        body.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Pending_file_is_consumed_state_is_initial_and_the_change_is_audited()
    {
        await using var factory = new B2ApiFactory();
        _ = factory.CreateClient();
        var keys = Path.Combine(factory.DataDirectory, "keys");

        File.Exists(AdminBootstrapFiles.PendingPath(factory.DataDirectory)).Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(keys, AdminBootstrapStore.StateFileName))).Should().Be("initial");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var audit = await db.AuditLogs.Where(a => a.EntityType == "User" && a.Reason == "F-01 per-install initial password").ToListAsync();
        audit.Should().ContainSingle();
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Without_an_installer_password_nobody_can_log_in_as_admin()
    {
        await using var factory = new B2ApiFactory { ProvisionAdminPassword = false };
        var client = factory.CreateClient();

        foreach (var password in new[] { AuthHttpExtensions.RetiredSharedSeedPassword, AuthHttpExtensions.InitialAdminPassword })
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", password));
            login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task After_the_admin_chooses_a_password_a_new_pending_one_is_ignored()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        await client.LoginAsAdminAsync(); // rotates to BootstrappedAdminPassword

        // Simulate an upgrade re-issuing an initial password, then the next service start.
        var keys = Path.Combine(factory.DataDirectory, "keys");
        AdminBootstrapFiles.StagePending(factory.DataDirectory, "Zz9x-Yy8w-Xx7v-Ww6u");
        using (var scope = factory.Services.CreateScope())
        {
            var (outcome, _) = await scope.ServiceProvider.GetRequiredService<AdminPasswordBootstrapper>()
                .RunAsync(CancellationToken.None);
            outcome.Should().Be(AdminBootstrapOutcome.AdminAlreadyRotated);
        }

        File.Exists(AdminBootstrapFiles.PendingPath(factory.DataDirectory)).Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(keys, AdminBootstrapStore.StateFileName))).Should().Be("rotated");
        var stillWorks = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin", AuthHttpExtensions.BootstrappedAdminPassword));
        stillWorks.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Admin_password_change_marks_state_rotated_immediately()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();

        await client.LoginAsAdminAsync(); // forced change → BootstrappedAdminPassword

        var state = Path.Combine(factory.DataDirectory, "keys", AdminBootstrapStore.StateFileName);
        (await File.ReadAllTextAsync(state)).Should().Be("rotated");
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public async Task Reset_by_another_admin_does_not_reopen_the_initial_password()
    {
        // Security review regression: rotate `admin`, have a second Admin reset it (sets
        // MustChangePassword again), stage a new initial password, restart — must be ignored.
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        (await client.PostAsJsonAsync(
            "/api/users", new UserCreateRequest("admin2", "Second Admin", UserRoleDto.Admin, "Temp-Admin2-Pw-1!")))
            .EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;
        var second = (await (await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin2", "Temp-Admin2-Pw-1!")))
            .Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        client.SetBearer(second.AccessToken);
        (await client.PostAsJsonAsync(
            "/api/auth/change-password", new ChangePasswordRequest("Temp-Admin2-Pw-1!", "Second-Admin-Pw-2!")))
            .EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;
        second = (await (await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin2", "Second-Admin-Pw-2!")))
            .Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options))!;
        client.SetBearer(second.AccessToken);
        (await client.PostAsJsonAsync(
            $"/api/users/{RetireSharedAdminSeedHash0004.SeedAdminId}/reset-password",
            new PasswordResetRequest("Temp-Reset-Pw-3!")))
            .EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = null;

        var keys = Path.Combine(factory.DataDirectory, "keys");
        AdminBootstrapFiles.StagePending(factory.DataDirectory, "Zz9x-Yy8w-Xx7v-Ww6u");
        using (var scope = factory.Services.CreateScope())
        {
            var (outcome, _) = await scope.ServiceProvider.GetRequiredService<AdminPasswordBootstrapper>()
                .RunAsync(CancellationToken.None);
            outcome.Should().Be(AdminBootstrapOutcome.AdminAlreadyRotated);
        }

        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Zz9x-Yy8w-Xx7v-Ww6u")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Temp-Reset-Pw-3!")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Feature", "F-01")]
    public void Migration_placeholder_matches_the_bootstrapper_constant()
    {
        RetireSharedAdminSeedHash0004.UnusablePlaceholderHash
            .Should().Be(AdminPasswordBootstrapper.RetiredSeedPlaceholderHash);
    }
}
