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

    public async Task<Guid?> TryConsumeAsync(string plainRefreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenHash.Sha256Hex(plainRefreshToken);
        var now = _clock.UtcNow;
        var row = await _db.RefreshTokens.AsNoTracking()
            .Where(r => r.TokenHash == hash && r.RevokedAt == null)
            .Select(r => new { r.Id, r.UserId, r.ExpiresAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null || row.ExpiresAt <= now)
        {
            return null;
        }

        // Compare-and-set: SQLite serialises writers, so of two concurrent refreshes with the same
        // token exactly one sees RevokedAt == null here and wins; the other gets 0 rows.
        var consumed = await _db.RefreshTokens
            .Where(r => r.Id == row.Id && r.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, now), cancellationToken);
        return consumed == 1 ? row.UserId : null;
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, now), cancellationToken);
    }
}
