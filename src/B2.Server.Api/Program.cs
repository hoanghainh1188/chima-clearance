using B2.Server.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
B2ApiHost.ConfigureServices(builder);
var app = builder.Build();
await B2ApiHost.InitializeDatabaseAsync(app);
B2ApiHost.MapPipeline(app);
app.Run();

namespace B2.Server.Api
{
    public partial class Program;
}
