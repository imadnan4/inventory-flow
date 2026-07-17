using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>
/// Represents a hashed refresh token issued to an authenticated user.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshToken"/> class.
    /// </summary>
    /// <param name="id">The refresh-token identifier.</param>
    /// <param name="userId">The identifier of the token owner.</param>
    /// <param name="tokenHash">The one-way hash of the issued refresh token.</param>
    /// <param name="expiresAtUtc">The UTC instant after which the token cannot be used.</param>
    /// <exception cref="DomainException">Thrown when an identifier or token hash is empty, or the expiration is not UTC.</exception>
    public RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc)
        : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Refresh token identifier is required.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("Refresh token user identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("Refresh token hash is required.");
        }

        if (expiresAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainException("Refresh token expiration must be in UTC.");
        }

        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>
    /// Gets the identifier of the user that owns this token.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Gets the one-way hash of the issued token.
    /// </summary>
    public string TokenHash { get; private set; }

    /// <summary>
    /// Gets the UTC instant at which this token expires.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Gets the UTC instant at which this token was revoked, if applicable.
    /// </summary>
    public DateTimeOffset? RevokedAtUtc { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this token is valid at the supplied UTC instant.
    /// </summary>
    /// <param name="utcNow">The current UTC instant.</param>
    /// <returns><see langword="true"/> when the token is unrevoked and unexpired; otherwise, <see langword="false"/>.</returns>
    public bool IsActive(DateTimeOffset utcNow) =>
        RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    /// <summary>
    /// Revokes this token at the supplied UTC instant.
    /// </summary>
    /// <param name="revokedAtUtc">The UTC instant at which the token was revoked.</param>
    /// <exception cref="DomainException">Thrown when the supplied instant is not UTC.</exception>
    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        if (revokedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new DomainException("Refresh token revocation time must be in UTC.");
        }

        RevokedAtUtc ??= revokedAtUtc;
    }
}
