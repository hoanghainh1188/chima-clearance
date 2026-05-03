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
        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        await db.IdempotencyKeys.Where(k => k.CreatedAt < cutoff).ExecuteDeleteAsync(context.CancellationToken);
    }
}
