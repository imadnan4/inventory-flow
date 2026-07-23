using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

public sealed class PurchaseReceiptTests
{
    [Fact]
    public void Constructor_NormalizesReplayKeyAndExposesImmutableReceipt()
    {
        var receipt = new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2.5000m,
            "  receive-1  ", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal("receive-1", receipt.IdempotencyKey);
        Assert.Equal(2.5000m, receipt.Quantity);
    }

    [Fact]
    public void Constructor_RejectsMissingIdentifiersInvalidQuantityAndNonUtcTime()
    {
        Assert.IsType<DomainException>(Record.Exception(() => new PurchaseReceipt(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1m, "key", Guid.NewGuid(), DateTimeOffset.UtcNow)));
        Assert.IsType<DomainException>(Record.Exception(() => new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1.00001m, "key", Guid.NewGuid(), DateTimeOffset.UtcNow)));
        Assert.IsType<DomainException>(Record.Exception(() => new PurchaseReceipt(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1m, "key", Guid.NewGuid(), DateTimeOffset.Now)));
    }
}
