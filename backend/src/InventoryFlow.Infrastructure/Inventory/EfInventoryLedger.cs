using System.Data;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Inventory;

/// <summary>Persists inventory movements and materialized balances atomically.</summary>
public sealed class EfInventoryLedger(ApplicationDbContext dbContext) : IInventoryLedger
{
    /// <inheritdoc />
    public async Task<InventoryMovement?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, InventoryMovementType type,
        decimal quantity, string idempotencyKey, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var existing = await dbContext.InventoryMovements.SingleOrDefaultAsync(movement =>
                movement.WorkspaceId == workspaceId && movement.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existing;
            }

            var warehouseExists = await dbContext.Warehouses.AnyAsync(warehouse => warehouse.Id == warehouseId &&
                warehouse.WorkspaceId == workspaceId && warehouse.ArchivedAtUtc == null, cancellationToken);
            var productExists = await dbContext.Products.AnyAsync(product => product.Id == productId &&
                product.WorkspaceId == workspaceId && product.ArchivedAtUtc == null, cancellationToken);
            if (!warehouseExists || !productExists)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var balance = await dbContext.InventoryBalances.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId &&
                item.WarehouseId == warehouseId && item.ProductId == productId, cancellationToken);
            if (balance is null)
            {
                balance = new InventoryBalance(workspaceId, warehouseId, productId, 0m);
                dbContext.InventoryBalances.Add(balance);
            }

            balance.Apply(type == InventoryMovementType.Receipt ? quantity : -quantity);
            var movement = new InventoryMovement(Guid.NewGuid(), workspaceId, warehouseId, productId, type, quantity,
                idempotencyKey, balance.Quantity, occurredAtUtc);
            dbContext.InventoryMovements.Add(movement);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return movement;
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InventoryBalance>> ListBalancesAsync(Guid workspaceId, Guid? warehouseId, Guid? productId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InventoryBalances.AsNoTracking().Where(balance => balance.WorkspaceId == workspaceId);
        if (warehouseId.HasValue) query = query.Where(balance => balance.WarehouseId == warehouseId.Value);
        if (productId.HasValue) query = query.Where(balance => balance.ProductId == productId.Value);
        return await query.OrderBy(balance => balance.WarehouseId).ThenBy(balance => balance.ProductId).ToListAsync(cancellationToken);
    }
}
