using System.Security.Cryptography;

namespace InventoryFlow.Infrastructure.Authentication;

/// <summary>Generates opaque refresh values and their one-way hashes.</summary>
public sealed class RefreshTokenGenerator
{
    /// <summary>Generates a 256-bit URL-safe opaque value.</summary>
    public string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    /// <summary>Hashes an opaque value for persistence.</summary>
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
