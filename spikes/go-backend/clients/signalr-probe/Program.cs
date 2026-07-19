// SignalR compatibility probe for the Go spike backend.
//
// Mirrors src/B2.Desktop/Services/ConnectivityService.cs wiring exactly:
//   HubConnectionBuilder().WithUrl(hubUri, o => o.AccessTokenProvider = ...).WithAutomaticReconnect()
//   connection.On<LedgerChangedPayload>("LedgerChanged", ...)
//
// PASS (exit 0)  = logged in, hub reached Connected, and a LedgerChanged event arrived.
// FAIL (exit 1)  = otherwise. Prints a clear verdict.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;

const string baseUrl = "http://127.0.0.1:5000/";
var hubUri = new Uri(new Uri(baseUrl), "hubs/events");

Console.WriteLine("[probe] logging in...");
using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
var loginResp = await http.PostAsJsonAsync("api/auth/login", new { username = "admin", password = "admin123" });
if (!loginResp.IsSuccessStatusCode)
{
    Console.WriteLine($"[probe] FAIL: login HTTP {(int)loginResp.StatusCode}");
    return 1;
}

var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
var login = await loginResp.Content.ReadFromJsonAsync<LoginResponse>(jsonOpts);
if (login is null || string.IsNullOrEmpty(login.AccessToken))
{
    Console.WriteLine("[probe] FAIL: no access token in login response");
    return 1;
}
Console.WriteLine($"[probe] logged in as {login.FullName} (role={login.Role}); token len={login.AccessToken.Length}");

// Identical to the desktop ConnectivityService wiring.
var connection = new HubConnectionBuilder()
    .WithUrl(hubUri, options => options.AccessTokenProvider = () => Task.FromResult<string?>(login.AccessToken))
    .WithAutomaticReconnect()
    .Build();

var gotEvent = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
connection.On<LedgerChangedPayload>("LedgerChanged", payload =>
{
    Console.WriteLine($"[probe] <-- LedgerChanged declarationId={payload.DeclarationId}");
    gotEvent.TrySetResult(payload.DeclarationId);
});

try
{
    Console.WriteLine($"[probe] connecting to {hubUri} ...");
    await connection.StartAsync();
    Console.WriteLine($"[probe] hub state = {connection.State}");
}
catch (Exception ex)
{
    Console.WriteLine($"[probe] FAIL: hub StartAsync threw: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

if (connection.State != HubConnectionState.Connected)
{
    Console.WriteLine($"[probe] FAIL: hub not Connected (state={connection.State})");
    return 1;
}

Console.WriteLine("[probe] connected; waiting up to 15s for a LedgerChanged push...");
var completed = await Task.WhenAny(gotEvent.Task, Task.Delay(TimeSpan.FromSeconds(15)));
await connection.DisposeAsync();

if (completed == gotEvent.Task)
{
    Console.WriteLine($"[probe] PASS: SignalR protocol-compatible. Received declarationId={gotEvent.Task.Result}");
    return 0;
}

Console.WriteLine("[probe] FAIL: connected but no LedgerChanged event within 15s");
return 1;

internal sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    bool MustChangePassword,
    string Role,
    Guid UserId,
    string FullName);

internal sealed class LedgerChangedPayload
{
    [JsonPropertyName("declarationId")]
    public Guid DeclarationId { get; init; }
}
