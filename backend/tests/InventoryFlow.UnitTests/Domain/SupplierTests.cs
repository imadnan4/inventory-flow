using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>Verifies supplier invariants.</summary>
public sealed class SupplierTests
{
    /// <summary>Normalizes the supplier name.</summary>
    [Fact]
    public void Constructor_NormalizesName()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "  Acme Supply  ", DateTimeOffset.UtcNow);
        Assert.Equal("Acme Supply", supplier.Name);
    }

    /// <summary>Rejects missing names and invalid identifiers or timestamps.</summary>
    [Fact]
    public void Constructor_WithInvalidInput_ThrowsDomainException()
    {
        Assert.IsType<DomainException>(Record.Exception(() => new Supplier(Guid.Empty, Guid.NewGuid(), "Supplier", DateTimeOffset.UtcNow)));
        Assert.IsType<DomainException>(Record.Exception(() => new Supplier(Guid.NewGuid(), Guid.NewGuid(), " ", DateTimeOffset.UtcNow)));
        Assert.IsType<DomainException>(Record.Exception(() => new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Supplier", DateTimeOffset.Now)));
    }

    /// <summary>Archives once without changing the original archive instant.</summary>
    [Fact]
    public void Archive_IsIdempotent()
    {
        var supplier = new Supplier(Guid.NewGuid(), Guid.NewGuid(), "Acme Supply", DateTimeOffset.UtcNow);
        var archivedAt = DateTimeOffset.UtcNow;
        supplier.Archive(archivedAt);
        supplier.Archive(archivedAt.AddMinutes(1));
        Assert.Equal(archivedAt, supplier.ArchivedAtUtc);
    }
}
