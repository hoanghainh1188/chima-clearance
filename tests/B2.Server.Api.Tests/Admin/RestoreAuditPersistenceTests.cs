using B2.Application.Abstractions;
using B2.Domain.Audit;
using B2.Infrastructure.Backup;
using B2.Infrastructure.DependencyInjection;
using B2.Infrastructure.Persistence;
using B2.Server.Api.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Admin;

public sealed class RestoreAuditPersistenceTests
{
    [Fact]
    [Trait("Feature", "F-15")]
    [Trait("Requirement", "FR-003")]
    public async Task Should_persist_restore_audit_into_database_after_apply()
    {
        await using var factory = new B2ApiFactory();
        var metadata = new RestoreStagingMetadata(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            new DateTimeOffset(2026, 6, 4, 10, 0, 0, TimeSpan.Zero),
            "b2-20260604-1000.bak");

        await RestoreAuditPersistence.PersistAfterApplyAsync(factory.Services, metadata);

        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var row = await db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.Action == AuditAction.Restore && a.EntityType == "DatabaseRestore");

        row.ActorUserId.Should().Be(metadata.ActorUserId);
        row.ChangesJson.Should().Contain("b2-20260604-1000.bak");
    }
}
