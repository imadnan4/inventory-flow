using System.ComponentModel.DataAnnotations;

namespace InventoryFlow.Infrastructure.Authentication;

/// <summary>Contains JWT and refresh-session settings.</summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Jwt";
    /// <summary>Gets or sets the trusted token issuer.</summary>
    [Required] public string Issuer { get; set; } = string.Empty;
    /// <summary>Gets or sets the intended token audience.</summary>
    [Required] public string Audience { get; set; } = string.Empty;
    /// <summary>Gets or sets the secret signing key, supplied outside tracked configuration.</summary>
    [Required, MinLength(32)] public string SigningKey { get; set; } = string.Empty;
    /// <summary>Gets or sets access-token lifetime in minutes.</summary>
    [Range(1, 60)] public int AccessTokenLifetimeMinutes { get; set; } = 10;
    /// <summary>Gets or sets refresh-token lifetime in days.</summary>
    [Range(1, 30)] public int RefreshTokenLifetimeDays { get; set; } = 7;
    /// <summary>Gets or sets the browser refresh cookie name.</summary>
    [Required] public string RefreshCookieName { get; set; } = "inventory_flow_refresh";
}
