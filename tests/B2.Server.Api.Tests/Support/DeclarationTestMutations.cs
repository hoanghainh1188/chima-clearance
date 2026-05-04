using B2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Support;

/// <summary>Direct SQL updates for integration tests that need states not yet exposed by public APIs in this sprint.</summary>
public static class DeclarationTestMutations
{
    public static async Task SetStatusKvgsConfirmedAsync(
        B2ApiFactory factory,
        Guid declarationId,
        Guid supervisorUserId,
        CancellationToken cancellationToken = default)
    {
        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE Declarations
             SET Status = 'KVGSConfirmed',
                 KvgsConfirmedByUserId = {supervisorUserId},
                 KvgsConfirmedAt = {now}
             WHERE Id = {declarationId}
             """,
            cancellationToken);
    }

    public static async Task SetStatusCancelledAsync(
        B2ApiFactory factory,
        Guid declarationId,
        Guid adminUserId,
        CancellationToken cancellationToken = default)
    {
        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE Declarations
             SET Status = 'Cancelled',
                 CancelledByUserId = {adminUserId},
                 CancelledAt = {now},
                 CancellationReason = 'test-cancel'
             WHERE Id = {declarationId}
             """,
            cancellationToken);
    }
}
