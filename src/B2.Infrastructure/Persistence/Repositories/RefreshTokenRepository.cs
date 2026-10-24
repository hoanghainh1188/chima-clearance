using B2.Application.Abstractions;
using B2.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly B2DbContext _db;
    private readonly IClock _clock;

    public RefreshTokenRepository(B2DbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task StoreAsync(Guid userId, string plainRefreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var hash = TokenHash.Sha256Hex(plainRefreshToken);
        await _db.RefreshTokens.AddAsync(
            new RefreshTokenRecord
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = hash,
                ExpiresAt = expiresAt,
                CreatedAt = _clock.UtcNow,
            },
            cancellationToken);
    }

    public async Task<Guid?> GetActiveUserIdAsync(string plainRefreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenHash.Sha256Hex(plainRefreshToken);
        var now = _clock.UtcNow;
        // The SQLite provider cannot translate DateTimeOffset comparisons (every /refresh used to
        // 500 here); a SHA-256 of a random token is effectively unique, so check the expiry on the single materialised row.
        var row = await _db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(
            r => r.TokenHash == hash && r.RevokedAt == null,
            cancellationToken);
        return row is not null && row.ExpiresAt > now ? row.UserId : null;
    }

    public async Task RevokeAsync(string plainRefreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenHash.Sha256Hex(plainRefreshToken);
        var row = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash && r.RevokedAt == null, cancellationToken);
        if (row is not null)
        {
            row.RevokedAt = _clock.UtcNow;
        }
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, now), cancellationToken);
    }
}
