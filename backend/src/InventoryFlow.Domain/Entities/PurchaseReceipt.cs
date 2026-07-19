using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>An immutable, supplier-linked posted goods receipt.</summary>
public sealed class PurchaseReceipt : Entity<Guid>
{
    public PurchaseReceipt(Guid id, Guid workspaceId, Guid supplierId, Guid warehouseId, Guid productId, decimal quantity,
        string idempotencyKey, Guid inventoryMovementId, DateTimeOffset receivedAtUtc) : base(id)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || supplierId == Guid.Empty || warehouseId == Guid.Empty ||
            productId == Guid.Empty || inventoryMovementId == Guid.Empty)
            throw new DomainException("Purchase receipt identifiers are required.");
        if (receivedAtUtc.Offset != TimeSpan.Zero) throw new DomainException("Purchase receipt time must be in UTC.");
        WorkspaceId = workspaceId;
        SupplierId = supplierId;
        WarehouseId = warehouseId;
        ProductId = productId;
        Quantity = InventoryMovement.ValidateQuantity(quantity);
        IdempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        InventoryMovementId = inventoryMovementId;
        ReceivedAtUtc = receivedAtUtc;
    }

    public Guid WorkspaceId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public string IdempotencyKey { get; private set; }
    public Guid InventoryMovementId { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; }
}
