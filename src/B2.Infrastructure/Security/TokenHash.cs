using System.Security.Cryptography;
using System.Text;

namespace B2.Infrastructure.Security;

public static class TokenHash
{
    public static string Sha256Hex(string plainText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToHexString(bytes);
    }
}
