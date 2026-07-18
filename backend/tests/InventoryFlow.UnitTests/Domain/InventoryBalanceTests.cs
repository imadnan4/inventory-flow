using InventoryFlow.Domain.Entities;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.UnitTests.Domain;

/// <summary>Verifies inventory ledger invariants.</summary>
public sealed class InventoryBalanceTests
{
    /// <summary>Applies four-decimal receipts and issues while retaining the expected balance.</summary>
    [Fact]
    public void Apply_ReceiptAndIssue_MaintainsPreciseNonnegativeBalance()
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m);
        balance.Apply(1.2345m);
        balance.Apply(-0.2345m);
        Assert.Equal(1.0000m, balance.Quantity);
    }

    /// <summary>Rejects an issue that would make stock negative.</summary>
    [Fact]
    public void Apply_IssueExceedingBalance_ThrowsInsufficientInventoryException()
    {
        var balance = new InventoryBalance(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1m);
        Assert.IsType<InsufficientInventoryException>(Record.Exception(() => balance.Apply(-1.0001m)));
    }

    /// <summary>Rejects unsupported precision and invalid idempotency input.</summary>
    [Fact]
    public void Movement_WithInvalidQuantityOrKey_ThrowsDomainException()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        Assert.IsType<DomainException>(Record.Exception(() => new InventoryMovement(ids[0], ids[1], ids[2], ids[3],
            InventoryMovementType.Receipt, 1.00001m, "key", 1m, DateTimeOffset.UtcNow)));
        Assert.IsType<DomainException>(Record.Exception(() => new InventoryMovement(ids[0], ids[1], ids[2], ids[3],
            InventoryMovementType.Receipt, 1m, " ", 1m, DateTimeOffset.UtcNow)));
    }
}
