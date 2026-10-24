using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private const string RefreshPath = "/api/auth/refresh";

    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    // Same shape as MauiProgram's Refit serializer: the server writes enums (Role) as strings,
    // so the default options throw while reading LoginResponse — after the server has already
    // rotated (revoked) the refresh token we sent.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
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

        var refreshed = await TryRefreshTokenAsync(request.RequestUri, refreshToken, cancellationToken).ConfigureAwait(false);
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
               path.Contains(RefreshPath, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> TryRefreshTokenAsync(Uri? originalUri, string refreshToken, CancellationToken cancellationToken)
    {
        await RefreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another request already rotated (or gave up on) this refresh token while we waited:
            // reuse its outcome instead of replaying a token the server has revoked.
            if (!string.Equals(_tokens.RefreshToken, refreshToken, StringComparison.Ordinal))
            {
                return !string.IsNullOrEmpty(_tokens.AccessToken);
            }

            // HttpClient resolves BaseAddress BEFORE the handler chain runs, so a request created
            // in here must already be absolute (a relative "/api/auth/refresh" never left the PC).
            if (originalUri is not { IsAbsoluteUri: true })
            {
                return false;
            }

            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(originalUri, RefreshPath))
            {
                Content = JsonContent.Create(new RefreshRequest(refreshToken)),
            };

            using var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken).ConfigureAwait(false);
            if (!refreshResponse.IsSuccessStatusCode)
            {
                if (refreshResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    // Refresh token expired / revoked / user deactivated: only a new login helps.
                    _tokens.ExpireSession();
                }

                return false;
            }

            var content = await refreshResponse.Content
                .ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (content is null || string.IsNullOrEmpty(content.AccessToken))
            {
                _tokens.ExpireSession();
                return false;
            }

            _tokens.AccessToken = content.AccessToken;
            _tokens.RefreshToken = content.RefreshToken;
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Transport failure or timeout: keep the tokens so the next request can retry.
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
