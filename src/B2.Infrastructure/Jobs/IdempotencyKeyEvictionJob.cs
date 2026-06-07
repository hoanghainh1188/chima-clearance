using B2.Application.Abstractions;
using B2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace B2.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class IdempotencyKeyEvictionJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public IdempotencyKeyEvictionJob(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var cutoff = clock.UtcNow.AddHours(-24);

        // SQLite cannot translate a DateTimeOffset comparison inside
        // ExecuteDelete, so materialize and filter the (small, 24h-bounded)
        // key set in memory, then remove the stale rows.
        var stale = (await db.IdempotencyKeys.ToListAsync(context.CancellationToken))
            .Where(k => k.CreatedAt < cutoff)
            .ToList();
        if (stale.Count > 0)
        {
            db.IdempotencyKeys.RemoveRange(stale);
            await db.SaveChangesAsync(context.CancellationToken);
        }
    }
}
