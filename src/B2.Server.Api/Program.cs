using B2.Server.Api;

var builder = WebApplication.CreateBuilder(args);

// Sprint S0: minimal API surface — health endpoint only. Real endpoint groups land in S1+.
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Anonymous health endpoint per `02-architecture.md § 6` and FR-001.
// Body remains minimal at S0; FR-018 fields land in S5 (T-S5-19).
app.MapGet("/api/admin/health", () => Results.Ok(new { status = "OK", uptime = "S0-bootstrap" }))
    .WithName("Health")
    .AllowAnonymous();

app.Run();

// Required for tests: `WebApplicationFactory<Program>` (Microsoft.AspNetCore.Mvc.Testing) and
// the AssemblyMarker pattern used by the ArchUnit layer-rules tests.
namespace B2.Server.Api
{
    public partial class Program;

    public static class AssemblyMarker;
}
