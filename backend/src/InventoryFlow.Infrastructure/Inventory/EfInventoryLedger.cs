using System.Data;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Inventory;

/// <summary>Persists inventory movements and materialized balances atomically.</summary>
public sealed class EfInventoryLedger(ApplicationDbContext dbContext) : IInventoryLedger
{
    public async Task<InventoryMovement?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, InventoryMovementType type,
        decimal quantity, string idempotencyKey, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var movement = await new InventoryLedgerWriter(dbContext).RecordAsync(workspaceId, warehouseId, productId, type, quantity,
                idempotencyKey, occurredAtUtc, Guid.NewGuid(), cancellationToken);
            if (movement is not null && dbContext.Entry(movement).State == EntityState.Added)
                await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return movement;
        });
    }

    public async Task<IReadOnlyList<InventoryBalance>> ListBalancesAsync(Guid workspaceId, Guid? warehouseId, Guid? productId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InventoryBalances.AsNoTracking().Where(balance => balance.WorkspaceId == workspaceId);
        if (warehouseId.HasValue) query = query.Where(balance => balance.WarehouseId == warehouseId.Value);
        if (productId.HasValue) query = query.Where(balance => balance.ProductId == productId.Value);
        return await query.OrderBy(balance => balance.WarehouseId).ThenBy(balance => balance.ProductId).ToListAsync(cancellationToken);
    }
}
