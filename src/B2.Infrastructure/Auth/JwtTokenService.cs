using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using B2.Application.Abstractions;
using B2.Domain.Users;
using Microsoft.IdentityModel.Tokens;

namespace B2.Infrastructure.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private const int AccessTokenHours = 8;
    private readonly RsaJwtKeyProvider _keyProvider;

    public JwtTokenService(RsaJwtKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public string CreateAccessToken(Guid userId, Role role, bool mustChangePassword, DateTimeOffset utcNow, out DateTimeOffset expiresAt)
    {
        expiresAt = utcNow.AddHours(AccessTokenHours);
        var key = _keyProvider.GetSigningKey();
        var creds = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("role", role.ToString()),
            new("must_change_password", mustChangePassword ? "true" : "false"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: utcNow.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
