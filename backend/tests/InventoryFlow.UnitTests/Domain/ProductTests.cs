using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>Verifies product invariants.</summary>
public sealed class ProductTests
{
    /// <summary>Normalizes product name and SKU.</summary>
    [Fact]
    public void Constructor_NormalizesNameAndSku()
    {
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), "  Widget  ", " ab- 1 ", DateTimeOffset.UtcNow);
        Assert.Equal("Widget", product.Name);
        Assert.Equal("AB- 1", product.Sku);
    }

    /// <summary>Rejects invalid identifiers, SKU, and timestamps.</summary>
    [Fact]
    public void Constructor_WithInvalidInput_ThrowsDomainException()
    {
        Assert.IsType<DomainException>(Record.Exception(() => new Product(Guid.Empty, Guid.NewGuid(), "Item", "SKU", DateTimeOffset.UtcNow)));
        Assert.IsType<DomainException>(Record.Exception(() => new Product(Guid.NewGuid(), Guid.NewGuid(), "Item", " ", DateTimeOffset.UtcNow)));
        Assert.IsType<DomainException>(Record.Exception(() => new Product(Guid.NewGuid(), Guid.NewGuid(), "Item", "SKU", DateTimeOffset.Now)));
    }

    /// <summary>Archives once without changing the original archive instant.</summary>
    [Fact]
    public void Archive_IsIdempotent()
    {
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), "Item", "SKU", DateTimeOffset.UtcNow);
        var archivedAt = DateTimeOffset.UtcNow;
        product.Archive(archivedAt);
        product.Archive(archivedAt.AddMinutes(1));
        Assert.Equal(archivedAt, product.ArchivedAtUtc);
    }
}
