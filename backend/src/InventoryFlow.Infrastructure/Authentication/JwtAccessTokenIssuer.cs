using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InventoryFlow.Application.Features.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InventoryFlow.Infrastructure.Authentication;

/// <summary>Issues short-lived signed access tokens.</summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    private readonly JwtOptions _options = options.Value;
    /// <summary>Creates an access token for a user.</summary>
    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(AuthenticatedUser user)
    {
        var expiresAtUtc = timeProvider.GetUtcNow().AddMinutes(_options.AccessTokenLifetimeMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email), new Claim("display_name", user.DisplayName) };
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, notBefore: timeProvider.GetUtcNow().UtcDateTime, expires: expiresAtUtc.UtcDateTime, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
