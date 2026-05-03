using B2.Application.Abstractions;
using B2.Infrastructure.Auth;
using B2.Infrastructure.Clock;
using B2.Infrastructure.Jobs;
using B2.Infrastructure.Persistence;
using B2.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        if (enableBackgroundJobs)
        {
            services.AddQuartz(q =>
            {
                var jobKey = new JobKey("IdempotencyKeyEviction");
                q.AddJob<IdempotencyKeyEvictionJob>(opts => opts.WithIdentity(jobKey));
                q.AddTrigger(opts => opts
                    .ForJob(jobKey)
                    .WithIdentity("IdempotencyKeyEviction-trigger")
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(30).RepeatForever())
                    .StartNow());
            });

            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
        }

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
