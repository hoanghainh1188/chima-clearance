namespace B2.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task StoreAsync(Guid userId, string plainRefreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically revokes a valid (not revoked, not expired) token and returns its user id, or
    /// <c>null</c> when the token is invalid or a concurrent caller consumed it first (#149).
    /// Takes effect immediately, independent of the unit of work.
    /// </summary>
    Task<Guid?> TryConsumeAsync(string plainRefreshToken, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
