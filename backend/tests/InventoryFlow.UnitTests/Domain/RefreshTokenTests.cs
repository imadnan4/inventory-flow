using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>
/// Verifies refresh-token domain invariants and lifecycle behavior.
/// </summary>
public sealed class RefreshTokenTests
{
    /// <summary>
    /// Treats an unrevoked token as active before it expires.
    /// </summary>
    [Fact]
    public void IsActive_WithUnrevokedUnexpiredToken_ReturnsTrue()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = CreateToken(now.AddDays(7));

        // Act
        var isActive = token.IsActive(now);

        // Assert
        Assert.True(isActive);
    }

    /// <summary>
    /// Treats a revoked token as inactive.
    /// </summary>
    [Fact]
    public void Revoke_WithUtcInstant_MarksTokenInactive()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var token = CreateToken(now.AddDays(7));

        // Act
        token.Revoke(now);

        // Assert
        Assert.False(token.IsActive(now));
        Assert.Equal(now, token.RevokedAtUtc);
    }

    /// <summary>
    /// Rejects empty user identifiers.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyUserIdentifier_ThrowsDomainException()
    {
        // Act
        var exception = Record.Exception(() => new RefreshToken(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "token-hash",
            DateTimeOffset.UtcNow.AddDays(7)));

        // Assert
        Assert.IsType<DomainException>(exception);
    }

    /// <summary>
    /// Rejects empty refresh-token family identifiers.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyFamilyIdentifier_ThrowsDomainException()
    {
        var exception = Record.Exception(() => new RefreshToken(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "token-hash", DateTimeOffset.UtcNow.AddDays(7)));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>
    /// Rejects empty workspace identifiers.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyWorkspaceIdentifier_ThrowsDomainException()
    {
        var exception = Record.Exception(() => new RefreshToken(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "token-hash", DateTimeOffset.UtcNow.AddDays(7)));

        Assert.IsType<DomainException>(exception);
    }

    /// <summary>
    /// Rejects refresh-token expiration times that are not UTC.
    /// </summary>
    [Fact]
    public void Constructor_WithNonUtcExpiration_ThrowsDomainException()
    {
        // Arrange
        var nonUtcExpiration = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(5));

        // Act
        var exception = Record.Exception(() => new RefreshToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "token-hash",
            nonUtcExpiration));

        // Assert
        Assert.IsType<DomainException>(exception);
    }

    private static RefreshToken CreateToken(DateTimeOffset expiresAtUtc) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "token-hash", expiresAtUtc);
}
