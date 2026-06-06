using B2.Application.Abstractions;
using B2.Application.Common;
using B2.Infrastructure.Archive;
using B2.Infrastructure.Auth;
using B2.Infrastructure.Backup;
using B2.Infrastructure.Clock;
using B2.Infrastructure.Diagnostics;
using B2.Infrastructure.Excel;
using B2.Infrastructure.Jobs;
using B2.Infrastructure.Persistence;
using B2.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace B2.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <param name="dataDirectory">ProgramData root (db, keys, logs base).</param>
    public static IServiceCollection AddB2Infrastructure(
        this IServiceCollection services,
        string connectionString,
        string dataDirectory,
        bool enableBackgroundJobs = true)
    {
        services.AddDbContext<B2DbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<B2DbContext>());
        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton(new RsaJwtKeyProvider(dataDirectory));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IProcessorRepository, ProcessorRepository>();
        services.AddScoped<IYardRepository, YardRepository>();
        services.AddScoped<IDeclarationRepository, DeclarationRepository>();
        services.AddScoped<IVehicleSuffixSearchRepository, VehicleSuffixSearchRepository>();
        services.AddScoped<IVehicleSuggestRepository, VehicleSuggestRepository>();
        services.AddScoped<IDailyHeaderRepository, DailyHeaderRepository>();
        services.AddScoped<IReportsRepository, ReportsRepository>();
        services.AddScoped<ISoB2ExportRepository, SoB2ExportRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IAnnualArchiveRepository, AnnualArchiveRepository>();
        services.AddSingleton<IAnnualArchiveWriter>(_ => new FileSystemArchiveWriter(Path.Combine(dataDirectory, "archive")));
        services.AddScoped<ISystemHealthProbe>(sp => new SystemHealthProbe(
            sp.GetRequiredService<B2DbContext>(),
            sp.GetRequiredService<IClock>(),
            Path.Combine(dataDirectory, "db", "b2.db")));
        services.AddSingleton<IBackupSnapshotService>(_ => new BackupSnapshotService(Path.Combine(dataDirectory, "db", "b2.db")));
        services.AddSingleton<IDatabaseRestoreService>(_ => new DatabaseRestoreService(Path.Combine(dataDirectory, "db", "b2.db")));
        services.AddSingleton<IBackupSecretsStore>(_ => new BackupSecretsStore(dataDirectory));
        services.AddSingleton<ISoB2Exporter>(_ => new ClosedXmlSoB2Exporter(dataDirectory));

        if (enableBackgroundJobs)
        {
            var vietnamTimeZone = VietnamTime.TimeZone;
            services.AddQuartz(q =>
            {
                var jobKey = new JobKey("IdempotencyKeyEviction");
                q.AddJob<IdempotencyKeyEvictionJob>(opts => opts.WithIdentity(jobKey));
                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("IdempotencyKeyEviction-trigger")
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(30).RepeatForever())
                    .StartNow());

                var headerJobKey = new JobKey("CreateDailyHeader");
                q.AddJob<CreateDailyHeaderJob>(opts => opts.WithIdentity(headerJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(headerJobKey)
                    .WithIdentity("CreateDailyHeader-trigger")
                    .WithCronSchedule("0 1 0 * * ?", x => x.InTimeZone(vietnamTimeZone)));

                var summaryJobKey = new JobKey("GenerateDailySummary");
                q.AddJob<GenerateDailySummaryJob>(opts => opts.WithIdentity(summaryJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(summaryJobKey)
                    .WithIdentity("GenerateDailySummary-trigger")
                    .WithCronSchedule("0 0 23 * * ?", x => x.InTimeZone(vietnamTimeZone)));

                // FR-021 — annual archive: 01:00 on 1 February (default; Admin-configurable).
                var archiveJobKey = new JobKey("AnnualArchive");
                q.AddJob<AnnualArchiveJob>(opts => opts.WithIdentity(archiveJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(archiveJobKey)
                    .WithIdentity("AnnualArchive-trigger")
                    .WithCronSchedule("0 0 1 1 2 ?", x => x.InTimeZone(vietnamTimeZone)));
            });

            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        }

        return services;
    }

    /// <summary>
    /// F-15 / T-S5-08 — binds <c>B2:Litestream</c> and registers the continuous-backup
    /// supervisor as a hosted service. Disabled by default; no-ops without the binary.
    /// </summary>
    public static IServiceCollection AddB2Litestream(
        this IServiceCollection services,
        IConfiguration configuration,
        string dataDirectory)
    {
        var section = configuration.GetSection("B2:Litestream");
        var dbPath = section["DbPath"];
        var options = new LitestreamOptions
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            ExecutablePath = section["ExecutablePath"] ?? string.Empty,
            DbPath = string.IsNullOrWhiteSpace(dbPath) ? Path.Combine(dataDirectory, "db", "b2.db") : dbPath,
        };

        foreach (var child in section.GetSection("Replicas").GetChildren())
        {
            var path = child["Path"];
            if (!string.IsNullOrWhiteSpace(path))
            {
                options.Replicas.Add(new LitestreamReplica(child["Name"] ?? string.Empty, path));
            }
        }

        services.AddSingleton(options);
        services.AddSingleton(sp => new LitestreamSupervisor(
            options,
            dataDirectory,
            sp.GetRequiredService<ILogger<LitestreamSupervisor>>()));
        services.AddHostedService(sp => sp.GetRequiredService<LitestreamSupervisor>());
        return services;
    }

    public static async Task EnsureDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<B2DbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        SqliteStartup.ApplyPragmasForContext(db);
    }
}
