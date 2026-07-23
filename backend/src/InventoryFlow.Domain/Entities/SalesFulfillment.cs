using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>An immutable posted single-line sales fulfillment.</summary>
public sealed class SalesFulfillment : Entity<Guid>
{
    public SalesFulfillment(Guid id, Guid workspaceId, Guid warehouseId, Guid productId, decimal quantity,
        string idempotencyKey, Guid inventoryMovementId, DateTimeOffset fulfilledAtUtc) : base(id)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || warehouseId == Guid.Empty || productId == Guid.Empty ||
            inventoryMovementId == Guid.Empty)
            throw new DomainException("Sales fulfillment identifiers are required.");
        if (fulfilledAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Sales fulfillment time must be in UTC.");
        WorkspaceId = workspaceId;
        WarehouseId = warehouseId;
        ProductId = productId;
        Quantity = InventoryMovement.ValidateQuantity(quantity);
        IdempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        InventoryMovementId = inventoryMovementId;
        FulfilledAtUtc = fulfilledAtUtc;
    }

    public Guid WorkspaceId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public string IdempotencyKey { get; private set; }
    public Guid InventoryMovementId { get; private set; }
    public DateTimeOffset FulfilledAtUtc { get; private set; }
}
