using FluentAssertions;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Tests.Features.Inventory;

public class InventoryMovementTests
{
    [Fact]
    public void MovementResponse_From_Maps_Properties_Correctly()
    {
        var movement = new InventoryMovement(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            InventoryMovementType.Receipt,
            5m,
            "key-1",
            100m,
            DateTimeOffset.UtcNow);

        var response = InventoryMovementResponse.From(movement);

        response.Id.Should().Be(movement.Id);
        response.WarehouseId.Should().Be(movement.WarehouseId);
        response.ProductId.Should().Be(movement.ProductId);
        response.Type.Should().Be(movement.Type);
        response.Quantity.Should().Be(5m);
        response.BalanceAfterQuantity.Should().Be(100m);
    }

    [Fact]
    public void MovementType_Receipt_Is_Defined()
    {
        Enum.IsDefined(InventoryMovementType.Receipt).Should().BeTrue();
    }

    [Fact]
    public void MovementType_Issue_Is_Defined()
    {
        Enum.IsDefined(InventoryMovementType.Issue).Should().BeTrue();
    }
}