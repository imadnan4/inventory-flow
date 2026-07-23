using InventoryFlow.Domain.Common;
using InventoryFlow.Domain.Exceptions;

namespace InventoryFlow.Domain.Entities;

/// <summary>An immutable posted single-product transfer between two warehouses.</summary>
public sealed class WarehouseTransfer : Entity<Guid>
{
    public WarehouseTransfer(Guid id, Guid workspaceId, Guid sourceWarehouseId, Guid destinationWarehouseId, Guid productId,
        decimal quantity, string idempotencyKey, Guid sourceInventoryMovementId, Guid destinationInventoryMovementId,
        DateTimeOffset transferredAtUtc) : base(id)
    {
        if (id == Guid.Empty || workspaceId == Guid.Empty || sourceWarehouseId == Guid.Empty || destinationWarehouseId == Guid.Empty ||
            productId == Guid.Empty || sourceInventoryMovementId == Guid.Empty || destinationInventoryMovementId == Guid.Empty)
            throw new DomainException("Warehouse transfer identifiers are required.");
        if (sourceWarehouseId == destinationWarehouseId)
            throw new DomainException("Source and destination warehouses must be different.");
        if (sourceInventoryMovementId == destinationInventoryMovementId)
            throw new DomainException("Warehouse transfer movements must be different.");
        if (transferredAtUtc.Offset != TimeSpan.Zero)
            throw new DomainException("Warehouse transfer time must be in UTC.");

        WorkspaceId = workspaceId;
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        ProductId = productId;
        Quantity = InventoryMovement.ValidateQuantity(quantity);
        IdempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        SourceInventoryMovementId = sourceInventoryMovementId;
        DestinationInventoryMovementId = destinationInventoryMovementId;
        TransferredAtUtc = transferredAtUtc;
    }

    public Guid WorkspaceId { get; private set; }
    public Guid SourceWarehouseId { get; private set; }
    public Guid DestinationWarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public string IdempotencyKey { get; private set; }
    public Guid SourceInventoryMovementId { get; private set; }
    public Guid DestinationInventoryMovementId { get; private set; }
    public DateTimeOffset TransferredAtUtc { get; private set; }
}
