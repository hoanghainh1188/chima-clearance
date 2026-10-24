namespace B2.Desktop.Services.Api;

/// <summary>
/// Shared holder for the current JWT access token. Registered as a singleton so
/// the (transient) <see cref="AuthTokenHandler"/> in the HttpClientFactory
/// pipeline, the SignalR access-token provider, and the login / logout flows all
/// observe the same value. The handler itself MUST stay transient — a singleton
/// <see cref="System.Net.Http.DelegatingHandler"/> is rejected by
/// HttpClientFactory ("InnerHandler must be null … must not be reused or
/// cached") when it rebuilds the handler chain — so token state lives here.
/// </summary>
public interface IAuthTokenStore
{
    string? AccessToken { get; set; }
    string? RefreshToken { get; set; }

    /// <summary>
    /// Raised (on a background thread) when the refresh token is rejected, so the shell can
    /// send the user back to login instead of failing every request with 401 (Bug #152).
    /// </summary>
    event EventHandler? SessionExpired;

    void Clear();

    /// <summary>Clears both tokens and raises <see cref="SessionExpired"/>.</summary>
    void ExpireSession();
}
