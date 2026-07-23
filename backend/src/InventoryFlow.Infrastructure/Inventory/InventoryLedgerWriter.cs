using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Inventory;

/// <summary>Shared transaction-aware inventory balance and movement writer.</summary>
internal sealed class InventoryLedgerWriter(ApplicationDbContext dbContext)
{
    internal async Task<InventoryMovement?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, InventoryMovementType type,
        decimal quantity, string idempotencyKey, DateTimeOffset occurredAtUtc, Guid movementId, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));

        var existing = await dbContext.InventoryMovements.SingleOrDefaultAsync(movement => movement.WorkspaceId == workspaceId &&
            movement.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.WarehouseId != warehouseId || existing.ProductId != productId ||
                existing.Type != type || existing.Quantity != quantity)
                throw new InvalidOperationException("Idempotency key reused with different parameters.");
            return existing;
        }

        var warehouseExists = await dbContext.Warehouses.AnyAsync(warehouse => warehouse.Id == warehouseId &&
            warehouse.WorkspaceId == workspaceId && warehouse.ArchivedAtUtc == null, cancellationToken);
        var productExists = await dbContext.Products.AnyAsync(product => product.Id == productId &&
            product.WorkspaceId == workspaceId && product.ArchivedAtUtc == null, cancellationToken);
        if (!warehouseExists || !productExists) return null;

        var balance = await dbContext.InventoryBalances.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId &&
            item.WarehouseId == warehouseId && item.ProductId == productId, cancellationToken);
        if (balance is null)
        {
            balance = new InventoryBalance(workspaceId, warehouseId, productId, 0m);
            dbContext.InventoryBalances.Add(balance);
        }

        balance.Apply(type == InventoryMovementType.Receipt ? quantity : -quantity);
        var movement = new InventoryMovement(movementId, workspaceId, warehouseId, productId, type, quantity, idempotencyKey,
            balance.Quantity, occurredAtUtc);
        dbContext.InventoryMovements.Add(movement);
        return movement;
    }
}
