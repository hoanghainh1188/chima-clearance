using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Application.DailyHeaders;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace B2.Infrastructure.Jobs;

[DisallowConcurrentExecution]
public sealed class GenerateDailySummaryJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;

    public GenerateDailySummaryJob(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var handler = scope.ServiceProvider.GetRequiredService<RecomputeDailySummaryCommandHandler>();
        var date = VietnamTime.BusinessDateFromUtc(clock.UtcNow);
        await handler.HandleAsync(new RecomputeDailySummaryCommand(date, "Scheduled summary"), context.CancellationToken);
    }
}
