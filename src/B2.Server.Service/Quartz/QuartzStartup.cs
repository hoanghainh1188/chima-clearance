using B2.Application.DailyHeaders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace B2.Server.Service.Quartz;

public static class QuartzStartup
{
    public static IServiceCollection AddB2QuartzStartup(this IServiceCollection services)
    {
        services.AddHostedService<DailyHeaderCatchupHostedService>();
        return services;
    }
}

internal sealed class DailyHeaderCatchupHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DailyHeaderCatchupHostedService(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ensureHeader = scope.ServiceProvider.GetRequiredService<EnsureTodayHeaderCommandHandler>();
        var summary = scope.ServiceProvider.GetRequiredService<RecomputeDailySummaryCommandHandler>();

        // Backfill any day(s) missed while the service was down, then recompute the
        // summary for each — covers a multi-day outage, not just today.
        var dates = await ensureHeader.EnsureCatchUpAsync(cancellationToken);
        foreach (var date in dates)
        {
            await summary.HandleAsync(new RecomputeDailySummaryCommand(date, "Startup catch-up"), cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
