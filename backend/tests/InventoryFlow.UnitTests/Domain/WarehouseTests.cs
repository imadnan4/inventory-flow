using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>Verifies warehouse invariants.</summary>
public sealed class WarehouseTests
{
    [Fact]
    public void Constructor_NormalizesName()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "  Central Depot  ", DateTimeOffset.UtcNow);
        Assert.Equal("Central Depot", warehouse.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsInvalidName(string name) =>
        Assert.Throws<DomainException>(() => new Warehouse(Guid.NewGuid(), Guid.NewGuid(), name, DateTimeOffset.UtcNow));

    [Fact]
    public void Archive_IsIdempotent()
    {
        var warehouse = new Warehouse(Guid.NewGuid(), Guid.NewGuid(), "Depot", DateTimeOffset.UtcNow);
        var first = DateTimeOffset.UtcNow;
        warehouse.Archive(first);
        warehouse.Archive(first.AddMinutes(1));
        Assert.Equal(first, warehouse.ArchivedAtUtc);
    }
}
