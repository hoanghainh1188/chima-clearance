using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace B2.Server.Api.Auth;

public static class HttpContextUserExtensions
{
    public static Guid? GetB2UserId(this ClaimsPrincipal user)
    {
        var s = user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return s is not null && Guid.TryParse(s, out var g) ? g : null;
    }
}
