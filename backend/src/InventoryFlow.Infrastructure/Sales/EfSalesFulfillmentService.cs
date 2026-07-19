using System.Data;
using InventoryFlow.Application.Features.Sales;
using InventoryFlow.Domain.Entities;
using InventoryFlow.Infrastructure.Inventory;
using InventoryFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryFlow.Infrastructure.Sales;

/// <summary>Posts immutable sales fulfillments and their issue ledger entries in one transaction.</summary>
public sealed class EfSalesFulfillmentService(ApplicationDbContext dbContext) : ISalesFulfillmentService
{
    public async Task<SalesFulfillment?> RecordAsync(Guid workspaceId, Guid warehouseId, Guid productId, decimal quantity,
        string idempotencyKey, DateTimeOffset fulfilledAtUtc, CancellationToken cancellationToken)
    {
        quantity = InventoryMovement.ValidateQuantity(quantity);
        idempotencyKey = InventoryMovement.NormalizeIdempotencyKey(idempotencyKey);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var existing = await dbContext.SalesFulfillments.SingleOrDefaultAsync(fulfillment => fulfillment.WorkspaceId == workspaceId &&
                fulfillment.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existing;
            }

            var fulfillmentId = Guid.NewGuid();
            var movement = await new InventoryLedgerWriter(dbContext).RecordAsync(workspaceId, warehouseId, productId,
                InventoryMovementType.Issue, quantity, fulfillmentId.ToString("N"), fulfilledAtUtc, Guid.NewGuid(), cancellationToken);
            if (movement is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            var fulfillment = new SalesFulfillment(fulfillmentId, workspaceId, warehouseId, productId, quantity, idempotencyKey,
                movement.Id, fulfilledAtUtc);
            dbContext.SalesFulfillments.Add(fulfillment);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return fulfillment;
        });
    }

    public async Task<IReadOnlyList<SalesFulfillment>> ListAsync(Guid workspaceId, CancellationToken cancellationToken) =>
        await dbContext.SalesFulfillments.AsNoTracking().Where(fulfillment => fulfillment.WorkspaceId == workspaceId)
            .OrderByDescending(fulfillment => fulfillment.FulfilledAtUtc).ThenByDescending(fulfillment => fulfillment.Id)
            .ToListAsync(cancellationToken);
}
