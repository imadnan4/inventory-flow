using InventoryFlow.Domain.Entities;

namespace InventoryFlow.Application.Features.Inventory;

/// <summary>Provides atomic workspace-scoped inventory ledger persistence.</summary>
public interface IInventoryLedger
{
    /// <summary>Records a movement once for an idempotency key, returning null when its product or warehouse is unavailable.</summary>
    Task<InventoryMovement?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, InventoryMovementType type,
        decimal quantity, string idempotencyKey, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken);

    /// <summary>Lists current balances, optionally restricted to a warehouse or product.</summary>
    Task<IReadOnlyList<InventoryBalance>> ListBalancesAsync(Guid workspaceId, Guid? warehouseId, Guid? productId,
        CancellationToken cancellationToken);
}
