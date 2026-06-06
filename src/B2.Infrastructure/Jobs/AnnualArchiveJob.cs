using B2.Application.Archive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace B2.Infrastructure.Jobs;

/// <summary>
/// FR-021 / INV-12 — annual archive Quartz job.
/// Default cron: <c>0 0 1 1 2 ?</c> = 01:00 on 1 February in Asia/Ho_Chi_Minh.
/// Delegates to <see cref="AnnualArchiveJobHandler"/> for the archive logic.
/// On failure it logs an Error (the Health page will surface it via the in-memory
/// Serilog sink); live data is NEVER deleted when verification fails.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AnnualArchiveJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnnualArchiveJob> _logger;

    public AnnualArchiveJob(IServiceScopeFactory scopeFactory, ILogger<AnnualArchiveJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<AnnualArchiveJobHandler>();

        _logger.LogInformation("Annual archive job started");

        var results = await handler.HandleAsync(context.CancellationToken);

        if (results.Count == 0)
        {
            _logger.LogInformation("Annual archive: no data older than 3 years found; nothing to archive");
            return;
        }

        foreach (var r in results)
        {
            if (r.Success)
                _logger.LogInformation(
                    "Annual archive {Year}: {Count} declarations archived successfully",
                    r.Year, r.DeclarationsArchived);
            else
                _logger.LogError(
                    "Annual archive {Year} FAILED — live data preserved. Reason: {Error}",
                    r.Year, r.ErrorMessage);
        }
    }
}
