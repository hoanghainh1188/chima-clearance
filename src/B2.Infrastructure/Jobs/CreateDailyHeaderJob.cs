using B2.Application.Abstractions;
using B2.Application.Common;
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
        using (var scope = _scopeFactory.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<EnsureTodayHeaderCommandHandler>();
            await handler.HandleAsync(context.CancellationToken);
        }

        // #184: the 23:00 summary misses vehicles entered 23:00–24:00. Once the day has
        // closed, finalize yesterday's TotalVehicles (separate scope = separate audit row).
        using (var scope = _scopeFactory.CreateScope())
        {
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var recompute = scope.ServiceProvider.GetRequiredService<RecomputeDailySummaryCommandHandler>();
            var yesterday = VietnamTime.BusinessDateFromUtc(clock.UtcNow).AddDays(-1);
            await recompute.HandleAsync(
                new RecomputeDailySummaryCommand(yesterday, "Scheduled end-of-day finalize"),
                context.CancellationToken);
        }
    }
}
