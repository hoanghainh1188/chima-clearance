using B2.Application.Abstractions;
using B2.Infrastructure.Diagnostics;
using B2.Server.Api.Hosting;
using B2.Server.Service.Quartz;
using B2.Server.Service.Serilog;
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

B2ApiHost.ConfigureServices(builder);
builder.Services.AddB2QuartzStartup();
var app = builder.Build();
await B2ApiHost.InitializeDatabaseAsync(app);
B2ApiHost.MapPipeline(app);
app.Run();
