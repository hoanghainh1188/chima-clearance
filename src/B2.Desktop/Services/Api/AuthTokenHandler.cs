using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using B2.Shared.Contracts.Auth;

namespace B2.Desktop.Services.Api;

/// <summary>
/// Attaches the current bearer token to outgoing requests and handles automatic
/// token refresh on HTTP 401 Unauthorized using the refresh token (FR-017 / Bug #152).
/// MUST be registered transient: HttpClientFactory owns the <see cref="DelegatingHandler"/>
/// lifetime and rebuilds the handler chain periodically; a cached/singleton
/// instance throws "InnerHandler must be null". Token state is read from the
/// singleton <see cref="IAuthTokenStore"/> instead of being held here.
/// </summary>
public sealed class AuthTokenHandler : DelegatingHandler
{
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);
    private readonly IAuthTokenStore _tokens;

    public AuthTokenHandler(IAuthTokenStore tokens) => _tokens = tokens;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _tokens.AccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Do not intercept 401 for auth endpoints (login, refresh) to avoid infinite loops
        if (response.StatusCode != HttpStatusCode.Unauthorized || IsAuthEndpoint(request.RequestUri))
        {
            return response;
        }

        var refreshToken = _tokens.RefreshToken;
        if (string.IsNullOrEmpty(refreshToken))
        {
            return response;
        }

        var refreshed = await TryRefreshTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);
        if (!refreshed || string.IsNullOrEmpty(_tokens.AccessToken))
        {
            return response;
        }

        // Retry original request with the new access token
        using var retryRequest = await CloneHttpRequestMessageAsync(request, cancellationToken).ConfigureAwait(false);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.AccessToken);
        response.Dispose();
        return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAuthEndpoint(Uri? uri)
    {
        var path = uri?.AbsolutePath ?? string.Empty;
        return path.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/api/auth/refresh", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> TryRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // If another thread already refreshed the token while we waited, succeed immediately
            if (_tokens.RefreshToken != refreshToken && !string.IsNullOrEmpty(_tokens.AccessToken))
            {
                return true;
            }

            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
            {
                Content = JsonContent.Create(new RefreshRequest(refreshToken))
            };

            var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken).ConfigureAwait(false);
            if (!refreshResponse.IsSuccessStatusCode)
            {
                _tokens.Clear();
                return false;
            }

            var content = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (content is null || string.IsNullOrEmpty(content.AccessToken))
            {
                _tokens.Clear();
                return false;
            }

            _tokens.AccessToken = content.AccessToken;
            _tokens.RefreshToken = content.RefreshToken;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri);

        if (req.Content != null)
        {
            var ms = new MemoryStream();
            await req.Content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);

            foreach (var h in req.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
        }

        foreach (var header in req.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
