namespace B2.Desktop.Services.Api;

/// <summary>
/// Default <see cref="IAuthTokenStore"/>. A plain singleton field — the desktop
/// is single-window and the token is set on the UI thread, so no extra
/// synchronization is required.
/// </summary>
public sealed class AuthTokenStore : IAuthTokenStore
{
    public string? AccessToken { get; set; }
}
