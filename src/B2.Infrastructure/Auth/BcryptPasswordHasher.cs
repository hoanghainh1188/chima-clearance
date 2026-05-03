using B2.Application.Abstractions;

namespace B2.Infrastructure.Auth;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 10;

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string storedHash) =>
        BCrypt.Net.BCrypt.Verify(password, storedHash);
}
