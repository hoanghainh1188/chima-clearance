using System.Net.Http.Headers;

namespace B2.Desktop.Services.Api;

/// <summary>
/// Attaches the current bearer token to outgoing requests. MUST be registered
/// transient: HttpClientFactory owns the <see cref="DelegatingHandler"/>
/// lifetime and rebuilds the handler chain periodically; a cached/singleton
/// instance throws "InnerHandler must be null". Token state is read from the
/// singleton <see cref="IAuthTokenStore"/> instead of being held here.
/// </summary>
public sealed class AuthTokenHandler : DelegatingHandler
{
    private readonly IAuthTokenStore _tokens;

    public AuthTokenHandler(IAuthTokenStore tokens) => _tokens = tokens;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _tokens.AccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
