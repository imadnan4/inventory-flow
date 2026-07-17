using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>
/// Verifies behavior shared by domain exceptions.
/// </summary>
public sealed class DomainExceptionTests
{
    /// <summary>
    /// Preserves the violated-invariant message.
    /// </summary>
    [Fact]
    public void Constructor_WithMessage_PreservesMessage()
    {
        // Arrange
        const string message = "Product SKU must be unique.";

        // Act
        var exception = new DomainException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }
}
