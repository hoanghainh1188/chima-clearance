using B2.Application.Abstractions;
using B2.Infrastructure.DependencyInjection;
using B2.Infrastructure.Diagnostics;
using B2.Server.Api.Hosting;
using B2.Server.Service.Quartz;
using B2.Server.Service.Serilog;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;

// One shared F-18 log buffer: Serilog writes into it, the /api/admin/logs endpoint reads
// it via ILogReader. Registered before B2ApiHost so its TryAdd default is skipped.
var logReader = new InMemoryLogReader();
var memorySink = new InMemoryLogStore(logReader);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(logReader);
builder.Services.AddSingleton<ILogReader>(logReader);
builder.Host.UseSerilog((ctx, _, cfg) => SerilogStartup.Configure(cfg, ctx.Configuration, memorySink));

if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

// Bug #173: only the SCM-hosted service can restart itself after a restore; `--console`
// keeps B2ApiHost's default (stop and let the developer start it again).
if (WindowsServiceHelpers.IsWindowsService())
{
    builder.Services.AddSingleton<IServiceRestarter, WindowsServiceRestarter>();
}

B2ApiHost.ConfigureServices(builder);
builder.Services.AddB2QuartzStartup();
builder.Services.AddB2Litestream(builder.Configuration, B2ApiHost.ResolveDataDirectory(builder));
var app = builder.Build();
await B2ApiHost.InitializeDatabaseAsync(app);
B2ApiHost.MapPipeline(app);
app.Run();
