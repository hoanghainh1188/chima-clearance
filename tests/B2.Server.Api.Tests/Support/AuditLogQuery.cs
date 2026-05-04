using B2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace B2.Server.Api.Tests.Support;

public static class AuditLogQuery
{
    public static async Task<int> CountForEntityAsync(B2ApiFactory factory, string entityType, Guid entityId)
    {
        using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        return await db.AuditLogs.CountAsync(
            a => a.EntityType == entityType && a.EntityId == entityId,
            CancellationToken.None);
    }
}
