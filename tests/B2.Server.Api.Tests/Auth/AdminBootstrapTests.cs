using System.Net;
using System.Net.Http.Json;
using B2.Application.Auth;
using B2.Infrastructure.Auth;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using B2.Shared.Contracts.Auth;
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

        File.Exists(Path.Combine(keys, AdminBootstrapStore.DevFileName)).Should().BeFalse();
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
        await File.WriteAllTextAsync(
            Path.Combine(keys, AdminBootstrapStore.DevFileName), "{\"Password\":\"Zz9x-Yy8w-Xx7v-Ww6u\"}");
        using (var scope = factory.Services.CreateScope())
        {
            var (outcome, _) = await scope.ServiceProvider.GetRequiredService<AdminPasswordBootstrapper>()
                .RunAsync(CancellationToken.None);
            outcome.Should().Be(AdminBootstrapOutcome.AdminAlreadyRotated);
        }

        File.Exists(Path.Combine(keys, AdminBootstrapStore.DevFileName)).Should().BeFalse();
        (await File.ReadAllTextAsync(Path.Combine(keys, AdminBootstrapStore.StateFileName))).Should().Be("rotated");
        var stillWorks = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("admin", AuthHttpExtensions.BootstrappedAdminPassword));
        stillWorks.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
