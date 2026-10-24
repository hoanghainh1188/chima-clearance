namespace B2.Desktop.Services.Api;

/// <summary>
/// Default <see cref="IAuthTokenStore"/>. A plain singleton field — the desktop
/// is single-window and the token is set on the UI thread, so no extra
/// synchronization is required.
/// </summary>
public sealed class AuthTokenStore : IAuthTokenStore
{
    private readonly object _gate = new();
    private string? _accessToken;
    private string? _refreshToken;

    public string? AccessToken
    {
        get { lock (_gate) return _accessToken; }
        set { lock (_gate) _accessToken = value; }
    }

    public string? RefreshToken
    {
        get { lock (_gate) return _refreshToken; }
        set { lock (_gate) _refreshToken = value; }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _accessToken = null;
            _refreshToken = null;
        }
    }
}
