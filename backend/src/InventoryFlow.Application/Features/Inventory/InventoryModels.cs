using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Inventory;

/// <summary>Represents a recorded inventory movement exposed to clients.</summary>
public sealed record InventoryMovementResponse(Guid Id, Guid WarehouseId, Guid ProductId, InventoryMovementType Type,
    decimal Quantity, decimal BalanceAfterQuantity, DateTimeOffset OccurredAtUtc)
{
    /// <summary>Maps a ledger entry to a response.</summary>
    public static InventoryMovementResponse From(InventoryMovement movement) => new(movement.Id, movement.WarehouseId,
        movement.ProductId, movement.Type, movement.Quantity, movement.BalanceAfterQuantity, movement.OccurredAtUtc);
}

/// <summary>Represents current on-hand inventory exposed to clients.</summary>
public sealed record InventoryBalanceResponse(Guid WarehouseId, Guid ProductId, decimal Quantity)
{
    /// <summary>Maps a balance to a response.</summary>
    public static InventoryBalanceResponse From(InventoryBalance balance) => new(balance.WarehouseId, balance.ProductId, balance.Quantity);
}
