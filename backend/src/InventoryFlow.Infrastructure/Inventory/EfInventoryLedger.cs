using System.Data;
using InventoryFlow.Application.Features.Inventory;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.Infrastructure.Inventory;

/// <summary>Persists inventory movements and materialized balances atomically.</summary>
public sealed class EfInventoryLedger(ApplicationDbContext dbContext, IServiceScopeFactory scopeFactory) : IInventoryLedger
{
    public async Task<InventoryMovement?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, InventoryMovementType type,
        decimal quantity, string idempotencyKey, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async (ct) =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var attemptCtx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaction = await attemptCtx.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var movement = await new InventoryLedgerWriter(attemptCtx).RecordAsync(workspaceId, warehouseId, productId, type, quantity,
                idempotencyKey, occurredAtUtc, Guid.NewGuid(), ct);
            if (movement is not null && attemptCtx.Entry(movement).State == EntityState.Added)
                await attemptCtx.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return movement;
        }, cancellationToken);
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
