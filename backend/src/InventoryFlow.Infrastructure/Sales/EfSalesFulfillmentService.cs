using System.Data;
using InventoryFlow.Application.Features.Sales;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Inventory;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryFlow.Infrastructure.Sales;

/// <summary>Posts immutable sales fulfillments and their issue ledger entries in one transaction.</summary>
public sealed class EfSalesFulfillmentService(ApplicationDbContext dbContext, IServiceScopeFactory scopeFactory) : ISalesFulfillmentService
{
    public async Task<SalesFulfillment?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, decimal quantity,
        string idempotencyKey, DateTimeOffset fulfilledAtUtc, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
            var strategy = dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                var attemptCtx = scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await using var transaction = await attemptCtx.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var existing = await attemptCtx.SalesFulfillments.SingleOrDefaultAsync(fulfillment => fulfillment.WorkspaceId == workspaceId &&
                    fulfillment.IdempotencyKey == idempotencyKey, cancellationToken);
                if (existing is not null)
                {
                    if (existing.WarehouseId != warehouseId || existing.ProductId != productId ||
                        existing.Quantity != quantity)
                        throw new InvalidOperationException("Idempotency key reused with different parameters.");
                    await transaction.CommitAsync(cancellationToken);
                    return existing;
                }

                var fulfillmentId = Guid.NewGuid();
                var movement = await new InventoryLedgerWriter(attemptCtx).RecordAsync(workspaceId, warehouseId, productId,
                    InventoryMovementType.Issue, quantity, fulfillmentId.ToString("N"), fulfilledAtUtc, Guid.NewGuid(), cancellationToken);
                if (movement is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return null;
                }

                var fulfillment = new SalesFulfillment(fulfillmentId, workspaceId, warehouseId, productId, quantity, idempotencyKey,
                    movement.Id, fulfilledAtUtc);
                attemptCtx.SalesFulfillments.Add(fulfillment);
                await attemptCtx.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return fulfillment;
            });
    }

    public async Task<IReadOnlyList<SalesFulfillment>> ListAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.SalesFulfillments.AsNoTracking().Where(fulfillment => fulfillment.WorkspaceId == workspaceId)
            .OrderByDescending(fulfillment => fulfillment.FulfilledAtUtc).ThenByDescending(fulfillment => fulfillment.Id)
            .ToListAsync(cancellationToken);
}
