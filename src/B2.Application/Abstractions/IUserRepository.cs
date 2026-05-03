using B2.Domain.Users;

namespace B2.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<(IReadOnlyList<User> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
