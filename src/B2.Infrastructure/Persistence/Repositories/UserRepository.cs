using B2.Application.Abstractions;
using B2.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace B2.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly B2DbContext _db;

    public UserRepository(B2DbContext db)
    {
        _db = db;
    }

    /// <summary>Tracked entity (login updates lockout / last login).</summary>
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var key = username.Trim().ToLowerInvariant();
        return _db.Users.FirstOrDefaultAsync(u => u.Username == key, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        var key = username.Trim().ToLowerInvariant();
        return _db.Users.AnyAsync(u => u.Username == key, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken) =>
        await _db.Users.AddAsync(user, cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var q = _db.Users.AsNoTracking().OrderBy(u => u.Username);
        var total = await q.CountAsync(cancellationToken);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, total);
    }
}
