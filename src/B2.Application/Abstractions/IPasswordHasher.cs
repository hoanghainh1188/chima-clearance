namespace B2.Application.Abstractions;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool Verify(string password, string storedHash);
}
