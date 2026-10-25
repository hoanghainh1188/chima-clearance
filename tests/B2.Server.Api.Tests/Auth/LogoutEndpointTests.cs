using System.Net;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Auth;

/// <summary>Security checklist § 10 / FR-003 — logout is a write and leaves an audit row.</summary>
public sealed class LogoutEndpointTests
{
    [Fact]
    [Trait("Feature", "F-01")]
    [Trait("Requirement", "FR-003")]
    public async Task Logout_revokes_refresh_tokens_and_writes_an_audit_row()
    {
        await using var factory = new B2ApiFactory();
        var client = factory.CreateClient();
        var admin = await client.LoginAsAdminAsync();
        client.SetBearer(admin.AccessToken);
        int before;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            before = await db.AuditLogs.CountAsync(a => a.EntityType == "User" && a.EntityId == SeedIds.AdminUserId);
        }

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
            var rows = await db.AuditLogs
                .Where(a => a.EntityType == "User" && a.EntityId == SeedIds.AdminUserId)
                .ToListAsync();
            rows.Should().HaveCount(before + 1);
            rows.Should().Contain(a => a.ChangesJson.Contains("refreshTokensRevoked") && a.ActorUserId == SeedIds.AdminUserId);
            (await db.RefreshTokens.CountAsync(t => t.UserId == SeedIds.AdminUserId && t.RevokedAt == null))
                .Should().Be(0);
        }
    }
}
