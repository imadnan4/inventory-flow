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
    /// <summary>Claim type that carries the active workspace identifier.</summary>
    public const string WorkspaceIdClaimType = "workspace_id";

    /// <summary>
    /// Claim type that carries the active workspace role. This is a NON-AUTHORITATIVE UI hint only;
    /// never use it for access control. The effective role is always revalidated from the database
    /// membership row per request by <c>CurrentWorkspaceResolver</c>.
    /// </summary>
    public const string WorkspaceRoleClaimType = "workspace_role";

    private readonly JwtOptions _options = options.Value;
    /// <summary>Creates an access token for a user.</summary>
    public (string Token, DateTimeOffset ExpiresAtUtc) Issue(AuthenticatedUser user)
    {
        var expiresAtUtc = timeProvider.GetUtcNow().AddMinutes(_options.AccessTokenLifetimeMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("display_name", user.DisplayName),
            new Claim(WorkspaceIdClaimType, user.Workspace.Id.ToString()),
            new Claim(WorkspaceRoleClaimType, user.Workspace.Role)
        };
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, notBefore: timeProvider.GetUtcNow().UtcDateTime, expires: expiresAtUtc.UtcDateTime, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
