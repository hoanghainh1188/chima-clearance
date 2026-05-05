using B2.Application.DailyHeaders;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace B2.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class CreateDailyHeaderJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public CreateDailyHeaderJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<EnsureTodayHeaderCommandHandler>();
        await handler.HandleAsync(context.CancellationToken);
    }
}
