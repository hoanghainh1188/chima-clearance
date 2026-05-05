using B2.Server.Api.Hosting;
using B2.Server.Service.Quartz;
using B2.Server.Service.Serilog;
using Serilog;

var memorySink = new InMemoryLogStore();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(memorySink);
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
