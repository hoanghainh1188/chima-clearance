using System.Text.Json.Serialization;
using B2.Application.DependencyInjection;
using B2.Application.DomainEvents;
using B2.Infrastructure.Auth;
using B2.Infrastructure.DependencyInjection;
using B2.Server.Api.Audit;
using B2.Server.Api.Endpoints;
using B2.Server.Api.Auth;
using B2.Server.Api.Middleware;
using B2.Server.Api.Realtime;
using B2.Application.Abstractions;
using B2.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace B2.Server.Api.Hosting;

public static class B2ApiHost
{
    public static string ResolveDataDirectory(WebApplicationBuilder builder)
    {
        var dataDir = builder.Configuration["B2:DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            // Production (Windows service): ProgramData. Dev on macOS/Linux: writable user-local path.
            var root = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dataDir = Path.Combine(root, "B2");
        }

        return dataDir;
    }

    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        var dataDir = ResolveDataDirectory(builder);
        Directory.CreateDirectory(Path.Combine(dataDir, "db"));
        var dbPath = Path.Combine(dataDir, "db", "b2.db");
        var connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared;Foreign Keys=True;Pooling=True";

        var enableJobs = !builder.Environment.IsEnvironment("Testing");

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAuditContext, HttpAuditContext>();

        // F-18 log buffer. TryAdd so the Windows Service can register the Serilog-connected
        // instance first (Program.cs) and win; the standalone API / tests get this default.
        builder.Services.TryAddSingleton<InMemoryLogReader>();
        builder.Services.TryAddSingleton<ILogReader>(sp => sp.GetRequiredService<InMemoryLogReader>());

        builder.Services.AddB2Application();
        builder.Services.AddB2Infrastructure(connectionString, dataDir, enableJobs);

        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<RsaJwtKeyProvider>((options, keyProvider) =>
            {
                // Keep short claim names ("role", "sub") so RequireRole matches JWT payload.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = keyProvider.GetSigningKey(),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = "role",
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/events"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddSignalR();
        builder.Services.AddSingleton<ISignalRDomainEventDispatcher, SignalRLedgerDispatcher>();

        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy(B2Policies.AdminOnly, p => p.RequireRole("Admin"));
            o.AddPolicy(B2Policies.GateOperator, p => p.RequireRole("Admin", "Operator"));
            o.AddPolicy(B2Policies.SupervisorOrAbove, p => p.RequireRole("Admin", "Supervisor"));
            o.AddPolicy(B2Policies.MasterRead, p => p.RequireRole("Admin", "Operator", "Supervisor"));
            o.AddPolicy(B2Policies.AnyAuthenticated, p => p.RequireAuthenticatedUser());
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    public static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        await app.Services.EnsureDatabaseAsync(cancellationToken);
    }

    public static void MapPipeline(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<RequirePasswordChangeMiddleware>();
        app.UseAuthorization();
        app.UseMiddleware<AuditPersistenceMiddleware>();
        app.UseMiddleware<IdempotencyMiddleware>();

        app.MapGet("/api/admin/health", () => Results.Ok(new { status = "OK", uptime = Environment.TickCount64 }))
            .WithName("Health")
            .AllowAnonymous();

        app.MapAuthEndpoints();
        app.MapUsersEndpoints();
        app.MapMasterDataEndpoints();
        app.MapDeclarationEndpoints();
        app.MapVehiclesEndpoints();
        app.MapDailyHeadersEndpoints();
        app.MapReportsEndpoints();
        app.MapAdminEndpoints();
        app.MapHub<EventsHub>("/hubs/events").RequireAuthorization();
    }
}
