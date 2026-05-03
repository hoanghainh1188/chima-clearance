namespace B2.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task StoreAsync(Guid userId, string plainRefreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>Returns user id when token is valid (not revoked, not expired).</summary>
    Task<Guid?> GetActiveUserIdAsync(string plainRefreshToken, CancellationToken cancellationToken);

    Task RevokeAsync(string plainRefreshToken, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
