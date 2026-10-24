using System.Net;
using System.Text;
using B2.Desktop.Services.Api;
using FluentAssertions;

namespace B2.Desktop.Tests.Services;

/// <summary>Bug #152 — expired access tokens must be refreshed transparently.</summary>
public sealed class AuthTokenHandlerTests
{
    private const string NewAccess = "new-access";
    private const string NewRefresh = "new-refresh";

    // The server serializes enums as strings (JsonStringEnumConverter in B2ApiHost).
    private const string RefreshBody =
        $$"""
        {"accessToken":"{{NewAccess}}","refreshToken":"{{NewRefresh}}",
         "accessTokenExpiresAt":"2026-10-24T10:00:00+00:00","mustChangePassword":false,
         "role":"Operator","userId":"6f1c1f3e-2a49-4d0e-9a52-0d9f4b3a1c11","fullName":"Nguyễn Văn A"}
        """;

    [Fact]
    public async Task Should_refresh_against_the_base_address_and_retry_with_the_new_token()
    {
        var server = new StubServer(_ => Json(HttpStatusCode.OK, RefreshBody));
        var (client, tokens) = CreateClient(server);

        var response = await client.GetAsync("/api/declarations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        server.RefreshUris.Should().ContainSingle()
            .Which.Should().Be(new Uri("http://gate-pc:5000/api/auth/refresh"));
        tokens.AccessToken.Should().Be(NewAccess);
        tokens.RefreshToken.Should().Be(NewRefresh);
    }

    [Fact]
    public async Task Should_expire_the_session_when_the_refresh_token_is_rejected()
    {
        var server = new StubServer(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var (client, tokens) = CreateClient(server);
        var expired = 0;
        tokens.SessionExpired += (_, _) => expired++;

        var response = await client.GetAsync("/api/declarations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        expired.Should().Be(1);
        tokens.AccessToken.Should().BeNull();
        tokens.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task Should_keep_tokens_when_the_refresh_call_cannot_reach_the_server()
    {
        var server = new StubServer(_ => throw new HttpRequestException("LAN down"));
        var (client, tokens) = CreateClient(server);
        var expired = 0;
        tokens.SessionExpired += (_, _) => expired++;

        var response = await client.GetAsync("/api/declarations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        expired.Should().Be(0);
        tokens.RefreshToken.Should().Be("old-refresh");
    }

    [Fact]
    public async Task Should_keep_tokens_when_the_server_fails_transiently()
    {
        var server = new StubServer(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var (client, tokens) = CreateClient(server);

        var response = await client.GetAsync("/api/declarations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        tokens.RefreshToken.Should().Be("old-refresh");
    }

    [Fact]
    public async Task Should_not_refresh_on_a_failed_login()
    {
        var server = new StubServer(_ => Json(HttpStatusCode.OK, RefreshBody));
        var (client, _) = CreateClient(server);

        var response = await client.PostAsync("/api/auth/login", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        server.RefreshUris.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_refresh_once_for_concurrent_unauthorized_requests()
    {
        var server = new StubServer(_ => Json(HttpStatusCode.OK, RefreshBody));
        var (client, _) = CreateClient(server);

        var responses = await Task.WhenAll(
            client.GetAsync("/api/declarations"),
            client.GetAsync("/api/daily-headers"),
            client.GetAsync("/api/health"));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        server.RefreshUris.Should().ContainSingle();
    }

    private static (HttpClient Client, AuthTokenStore Tokens) CreateClient(StubServer server)
    {
        var tokens = new AuthTokenStore { AccessToken = "expired-access", RefreshToken = "old-refresh" };
        var client = new HttpClient(new AuthTokenHandler(tokens) { InnerHandler = server })
        {
            BaseAddress = new Uri("http://gate-pc:5000/"),
        };
        return (client, tokens);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// Fake LAN server: API calls succeed only with <see cref="NewAccess"/>; login always
    /// fails; refresh calls go to <c>onRefresh</c>. Reading <see cref="Uri.AbsolutePath"/>
    /// throws for a relative URI, exactly like the real transport would.
    /// </summary>
    private sealed class StubServer : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _onRefresh;
        private readonly List<Uri> _refreshUris = [];

        public StubServer(Func<HttpRequestMessage, HttpResponseMessage> onRefresh) => _onRefresh = onRefresh;

        public IReadOnlyList<Uri> RefreshUris
        {
            get
            {
                lock (_refreshUris)
                {
                    return _refreshUris.ToList();
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/auth/refresh")
            {
                lock (_refreshUris)
                {
                    _refreshUris.Add(request.RequestUri);
                }

                // Widen the race window for the concurrency test.
                await Task.Delay(50, cancellationToken);
                return _onRefresh(request);
            }

            if (path == "/api/auth/login")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return request.Headers.Authorization?.Parameter == NewAccess
                ? new HttpResponseMessage(HttpStatusCode.OK)
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }
    }
}
